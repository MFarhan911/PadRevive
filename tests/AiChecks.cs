using GenericPadBridge;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

public static class AiChecks
{
    public static async Task Run(Action<bool,string> check,Action<Action,string> reject)
    {
        InputSnapshot Raw(int x=32768,int y=32768)=>new(new bool[12],new Dictionary<AxisSource,int>{{AxisSource.X,x},{AxisSource.Y,y}},[-1]);
        var learning=new DriftLearningCapture(AxisSource.X,AxisSource.Y);
        reject(()=>learning.Build(),"drift learner refuses a correction before measurement");
        for(int i=0;i<=150;i++)learning.Observe(Raw(34768+i%7,31768+i%5),i*.02);
        check(learning.Phase==DriftLearningPhase.Rim&&learning.RestSamples==151,"drift learner waits for three seconds and sufficient rest samples");
        check(Math.Abs(learning.CentreX-34771)<=1&&Math.Abs(learning.CentreY-31770)<=1,"robust fit learns each shifted rest centre");
        reject(()=>learning.Build(),"drift learner refuses to invent unmeasured rim travel");
        for(int direction=0;direction<16;direction++)for(int i=0;i<7;i++)
        {
            double angle=direction*Math.PI/8,radius=direction==0?.65:.82;
            learning.Observe(Raw((int)(learning.CentreX+Math.Cos(angle)*radius*32767.5),(int)(learning.CentreY+Math.Sin(angle)*radius*32767.5)),4+direction+i*.01);
        }
        check(learning.CoveredDirections==16,"drift learner requires coverage of all sixteen directions");
        check(Math.Abs(learning.Summary.RestSeconds-3)<.001,"rest duration excludes the later rim sweep from the adviser summary");
        var correction=learning.Build();var profile=new MappingProfile{LeftRescue=correction};profile.Validate();
        var neutral=Mapper.Map(profile,Raw((int)learning.CentreX,(int)learning.CentreY));check(neutral.LX==0&&neutral.LY==0,"learned drift correction neutralises the measured shifted centre");
        var full=Mapper.Map(profile,Raw((int)(learning.CentreX+.65*32767.5),(int)learning.CentreY));check(full.LX>32700&&Math.Abs(full.LY)<10,"learned weak rim direction reaches full usable output");
        var saved=ProfileData.Clone(profile);saved.Validate();check(saved.LeftRescue.CentreX==correction.CentreX&&saved.LeftRescue.Enabled,"learned tuning persists through existing profile storage");
        var resilient=new DriftLearningCapture(AxisSource.X,AxisSource.Y);
        for(int i=0;i<=150;i++)resilient.Observe(i==75?Raw(61000,2000):Raw(34000+i%3,32000+i%3),i*.02);
        check(resilient.Phase==DriftLearningPhase.Rim&&resilient.NoiseRadius<.001,"one isolated rest spike does not set the centre or inflate the noise model");
        for(int direction=0;direction<16;direction++)for(int i=0;i<7;i++)
        {
            double angle=direction*Math.PI/8,radius=.75;
            resilient.Observe(Raw((int)(resilient.CentreX+Math.Cos(angle)*radius*32767.5),(int)(resilient.CentreY+Math.Sin(angle)*radius*32767.5)),4+direction+i*.01);
        }
        resilient.Observe(Raw(65535,(int)resilient.CentreY),21);var robust=resilient.Build();check(robust.Outer[0]<.76,"isolated rim spikes are excluded from measured travel");
        var moved=new DriftLearningCapture(AxisSource.X,AxisSource.Y);bool movementRejected=false;
        try{for(int i=0;i<=150;i++)moved.Observe(Raw(i%2==0?15000:50000,32768),i*.02);}catch(System.IO.InvalidDataException){movementRejected=true;}
        check(movementRejected,"moving during rest capture is rejected without applying a model");
        reject(()=>new DriftLearningCapture(AxisSource.X,AxisSource.X),"drift learning rejects duplicate axes");
        var missing=new DriftLearningCapture(AxisSource.X,AxisSource.Y);missing.Observe(new([],new Dictionary<AxisSource,int>(),[]),1);check(missing.RestSamples==0,"missing axis reports never become invented calibration samples");
        reject(()=>missing.Observe(Raw(-1,32768),1),"invalid raw stick values are rejected");
        check(DriftLearningCapture.Quantile(new[]{1d,4,2,3},.5)==2.5,"rest median handles an even number of observations");

        long Tick(double milliseconds)=>(long)Math.Round(milliseconds/1000*Stopwatch.Frequency);
        var timing=new BridgeTiming();long origin=Tick(1000);
        for(int i=0;i<100;i++){long begin=origin+Tick(i*8);timing.Record(begin,begin+Tick(1),begin+Tick(3),true,true,true);}
        var measured=timing.Snapshot(origin+Tick(800));
        check(Math.Abs(measured.ProcessingMeanMs-1)<.001&&Math.Abs(measured.ProcessingP95Ms-1)<.001,"timing measures input-to-submission work without including subsequent motor work");
        check(Math.Abs(measured.IntervalMeanMs-8)<.001&&Math.Abs(measured.ObservedHz-125)<.001,"poll spacing and observed app rate are measured independently");
        timing.Record(origin+Tick(1000),origin+Tick(1000),origin+Tick(1001),false,true,false);timing.Record(origin+Tick(1008),origin+Tick(1008),origin+Tick(1009),true,true,false);timing.Record(origin+Tick(1016),origin+Tick(1016),origin+Tick(1017),true,false,false);
        var errors=timing.Snapshot(origin+Tick(1020));check(errors.ReadErrors==1&&errors.OutputErrors==1&&errors.LatePolls==1,"read and output errors are counted separately and paused output is not an error");
        check(errors.Samples==100&&errors.LastSuccessAgeMs>200,"failed or paused polls do not invent successful submission timings");
        for(int i=0;i<800;i++){long begin=origin+Tick(2000+i*8);timing.Record(begin,begin+Tick(.5),begin+Tick(.6),true,true,true);}
        var bounded=timing.Snapshot();check(bounded.Samples==512&&bounded.RecentIntervals.Length==120,"timing history remains bounded during long sessions");
        timing.Reset();check(!timing.Snapshot().HasData&&timing.Snapshot().ReadErrors==0,"timing reset clears history and error counts");
        var report=new ReviveAiReport(EmulatedController.Xbox360,true,true,false,true,true,8,true,1,1,false,1,1,false,false,learning.Summary,measured);
        check(ReviveAdvisor.Inspect(report).Single().Title=="Checks look steady","local profile insights distinguish evidence from hardware verification");
        check(ReviveAdvisor.Inspect(report with{StickMouse=true}).Any(f=>f.Title=="Mixed input"),"profile coach identifies simultaneous pointer and gamepad output");
        check(ReviveAdvisor.Inspect(report with{ForwardRumble=false}).Any(f=>f.Title=="Game vibration muted"),"rumble adviser distinguishes profile muting from a broken motor");
        check(ReviveAdvisor.Inspect(report with{MotorBackend=false}).Any(f=>f.Title=="Motor output unavailable"),"rumble adviser reports an unavailable backend honestly");
        check(ReviveAdvisor.Inspect(report with{MotorBackend=false,MotorOwned=false}).Any(f=>f.Title=="Shared motors"),"rumble adviser distinguishes shared motor ownership from a broken backend");
        check(ReviveAdvisor.Inspect(report with{Timing=errors}).Any(f=>f.Title=="Read errors recorded"),"timing adviser includes measured input errors");

        string request=ReviveAiClient.BuildRequest(report,ReviveAiTask.StickTuning,ReviveAiClient.DefaultModel);using(var document=JsonDocument.Parse(request))
        {
            var root=document.RootElement;check(root.GetProperty("model").GetString()=="gpt-6-astra"&&!root.GetProperty("store").GetBoolean(),"OpenAI request uses the configured model and disables response storage");
            check(root.GetProperty("text").GetProperty("format").GetProperty("type").GetString()=="json_schema"&&root.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean(),"adviser asks for a strict data-only answer");
            using var data=JsonDocument.Parse(root.GetProperty("input")[1].GetProperty("content").GetString()!);
            check(!data.RootElement.EnumerateObject().Any(p=>p.Name is "device" or "path" or "profileName" or "apiKey")&&!request.Contains("OPENAI_API_KEY"),"adviser context excludes identifiers, paths, profile names and credentials");
        }
        reject(()=>ReviveAiClient.BuildRequest(report,ReviveAiTask.StickTuning,"unconfigured-model"),"unconfigured models cannot silently send requests elsewhere");
        var advice=new ReviveAiAdvice("Measured rest can be corrected.",[new("Measured offset","Preview the fitted curve before applying it.","info")],["Inspect the preview."]);
        string Response(string status="completed",string? text=null)=>JsonSerializer.Serialize(new {status,output=new object[]{new {type="reasoning"},new {type="message",content=new[]{new {type="output_text",text=text??JsonSerializer.Serialize(advice,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase})}}}}});
        check(ReviveAiClient.ParseResponse(Response()).NextSteps.Single()=="Inspect the preview.","Responses adapter reads message content after reasoning items");
        reject(()=>ReviveAiClient.ParseResponse(Response("incomplete")),"incomplete AI replies cannot become advice");
        reject(()=>ReviveAiClient.ParseResponse("{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"refusal\",\"refusal\":\"No\"}]}]}"),"AI refusal is handled without applying settings");
        reject(()=>ReviveAiClient.ParseResponse(Response(text:"{\"summary\":\"x\",\"findings\":[],\"next_steps\":[],\"run_command\":\"bad\"}")),"unexpected AI instructions are rejected as extra fields");
        reject(()=>new ReviveAiAdvice("x",[new("x","x","critical")],[]).Validate(),"AI finding severity is validated locally");
        var handler=new FakeAiHandler(Response());using(var client=new ReviveAiClient(handler))
        {
            var received=await client.Analyze(report,ReviveAiTask.InputConflicts,"chat-latest","test-only-api-key-for-fixture");
            check(received.Summary==advice.Summary&&handler.Url=="https://api.openai.com/v1/responses","AI adapter uses the fixed OpenAI HTTPS endpoint");
            check(handler.Authorization=="Bearer test-only-api-key-for-fixture"&&!handler.Body.Contains("test-only-api-key-for-fixture"),"API key is sent only as authorization, not inside controller data");
        }
        bool unauthorized=false;using(var client=new ReviveAiClient(new FakeAiHandler("private server details",HttpStatusCode.Unauthorized)))try{await client.Analyze(report,ReviveAiTask.StickTuning,"chat-latest","test-only-api-key-for-fixture");}catch(HttpRequestException error){unauthorized=!error.Message.Contains("private server details");}check(unauthorized,"AI connection failures do not expose response bodies or credentials");
        bool cancelled=false;using(var client=new ReviveAiClient(new FakeAiHandler(Response(),delay:true)))using(var cancel=new CancellationTokenSource(20))try{await client.Analyze(report,ReviveAiTask.StickTuning,"chat-latest","test-only-api-key-for-fixture",cancel.Token);}catch(OperationCanceledException){cancelled=true;}check(cancelled,"AI work can be cancelled without blocking the input loop");
    }
    private sealed class FakeAiHandler(string body,HttpStatusCode status=HttpStatusCode.OK,bool delay=false):HttpMessageHandler
    {
        public string Url="",Authorization="",Body="";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            Url=request.RequestUri!.AbsoluteUri;Authorization=request.Headers.Authorization!.ToString();Body=await request.Content!.ReadAsStringAsync(cancellation);
            if(delay)await Task.Delay(Timeout.Infinite,cancellation);
            return new(status){Content=new StringContent(body,Encoding.UTF8,"application/json")};
        }
    }
}
