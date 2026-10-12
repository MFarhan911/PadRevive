namespace GenericPadBridge;

public sealed record RumbleLayout(ushort Vendor,ushort Product,ushort UsagePage,ushort Usage,ushort BitSize,ushort Count,byte ReportId,ushort ReportLength,int LogicalMin,int LogicalMax,bool IsRange,ushort OutputValueCaps,ushort OutputButtonCaps);
public static class RumbleProtocol
{
    // Protocol data: PantherLord family, Linux drivers/hid/hid-pl.c.
    public static bool Supports(RumbleLayout d)
    {
        if(d.Vendor!=0x0810||d.Product!=0x0001||d.UsagePage!=0xff00||d.Usage!=2||d.IsRange||d.Count!=4||d.LogicalMin!=0||d.LogicalMax<127||d.OutputValueCaps!=1||d.OutputButtonCaps!=0)return false;
        // Preserve the original 16-bit layout, and accept the exact 8-bit variant
        // reported by the user's controller. Never mix a width with the wrong length.
        return (d.BitSize==16&&d.ReportLength==9&&d.LogicalMax<=ushort.MaxValue)
            ||(d.BitSize==8&&d.ReportLength==5&&d.ReportId==1&&d.LogicalMax==255);
    }
    public static byte[] ValueArray(RumbleLayout layout,byte strong,byte weak)
    {
        if(!Supports(layout))throw new InvalidOperationException("The controller's output descriptor does not match the supported 0810:0001 protocol.");
        int stride=layout.BitSize/8;
        byte[] values=new byte[layout.Count*stride];
        // Four values: reserved zero, reserved zero, strong 0..127, weak 0..127.
        // Motor semantics come from the family protocol; a logical max of 255
        // does not establish that the motors should receive values above 127.
        values[2*stride]=(byte)(strong*127/255);values[3*stride]=(byte)(weak*127/255);
        return values;
    }
    public static (byte Strong,byte Weak) Mix(MappingProfile p,byte strong,byte weak,bool directTest=false)
    {
        if(!directTest&&!p.ForwardRumble)return (0,0);
        if(directTest)return (strong,weak);
        byte s=(byte)Math.Clamp((int)Math.Round(strong*p.StrongGain),0,255),w=(byte)Math.Clamp((int)Math.Round(weak*p.WeakGain),0,255);
        return p.Routing switch {"swap"=>(w,s),"mono"=>(Math.Max(s,w),Math.Max(s,w)),_=>(s,w)};
    }
}
