namespace GenericPadBridge;

public static class DevicePresets
{
    public static bool Sony(ControllerChoice d)=>d.Vendor==0x054C&&new ushort[]{0x05C4,0x09CC,0x0CE6,0x0DF2}.Contains(d.Product);
    public static string Support(ControllerChoice d)=>d.XInputIndex>=0?"XInput · native vibration · verify on this device":Sony(d)?"PlayStation input preset · USB/Bluetooth via Windows · rumble not yet implemented":"DirectInput · buttons / POV / axes · rumble depends on the device";
    public static MappingProfile Create(ControllerChoice d,HashSet<AxisSource> sources,int count,int povs)
    {
        var p=new MappingProfile();
        if(d.XInputIndex>=0)
        {
            p.Name="XInput standard layout";
            p.Buttons=[ButtonTarget.A,ButtonTarget.B,ButtonTarget.X,ButtonTarget.Y,ButtonTarget.LB,ButtonTarget.RB,ButtonTarget.Back,ButtonTarget.Start,ButtonTarget.LS,ButtonTarget.RS];
            p.VisualButtons=[3,1,0,2,4,5,-1,-1,6,7,8,9];
            p.RightX=AxisSource.RotationX;p.RightY=AxisSource.RotationY;
            p.LeftTrigger=new(){Source=AxisSource.Slider0,Target=ButtonTarget.LT};p.RightTrigger=new(){Source=AxisSource.Slider1,Target=ButtonTarget.RT};
        }
        else if(Sony(d))
        {
            p.Name=d.Product is 0x0CE6 or 0x0DF2?"DualSense input preset":"DualShock 4 input preset";
            p.Buttons=[ButtonTarget.X,ButtonTarget.A,ButtonTarget.B,ButtonTarget.Y,ButtonTarget.LB,ButtonTarget.RB,ButtonTarget.Unmapped,ButtonTarget.Unmapped,ButtonTarget.Back,ButtonTarget.Start,ButtonTarget.LS,ButtonTarget.RS,ButtonTarget.Guide,ButtonTarget.Unmapped];
            p.VisualButtons=[3,2,1,0,4,5,6,7,8,9,10,11];
            p.VisualGuideButton=12;p.VisualTouchpadButton=13;
            p.RightX=sources.Contains(AxisSource.Z)?AxisSource.Z:AxisSource.None;p.RightY=sources.Contains(AxisSource.RotationZ)?AxisSource.RotationZ:AxisSource.None;
            if(sources.Contains(AxisSource.RotationX))p.LeftTrigger=new(){Source=AxisSource.RotationX,Target=ButtonTarget.LT};
            else p.Buttons[6]=ButtonTarget.LT;
            if(sources.Contains(AxisSource.RotationY))p.RightTrigger=new(){Source=AxisSource.RotationY,Target=ButtonTarget.RT};
            else p.Buttons[7]=ButtonTarget.RT;
        }
        else
        {
            if(d.Vendor!=0x0810||d.Product!=1){p.Name="Generic USB starter · run controller setup";p.Buttons=[ButtonTarget.A,ButtonTarget.B,ButtonTarget.X,ButtonTarget.Y,ButtonTarget.LB,ButtonTarget.RB,ButtonTarget.LT,ButtonTarget.RT,ButtonTarget.Back,ButtonTarget.Start,ButtonTarget.LS,ButtonTarget.RS];p.VisualButtons=[3,1,0,2,4,5,6,7,8,9,10,11];}
            if(sources.Contains(AxisSource.RotationX)&&sources.Contains(AxisSource.RotationY)){p.RightX=AxisSource.RotationX;p.RightY=AxisSource.RotationY;}
            else if(sources.Contains(AxisSource.RotationZ)&&sources.Contains(AxisSource.Z)){p.RightX=AxisSource.RotationZ;p.RightY=AxisSource.Z;}
            else {p.RightX=AxisSource.None;p.RightY=AxisSource.None;}
        }
        p.Buttons=Enumerable.Range(0,Math.Clamp(count,1,128)).Select(i=>i<p.Buttons.Length?p.Buttons[i]:ButtonTarget.Unmapped).ToArray();
        p.LeftX=sources.Contains(AxisSource.X)?AxisSource.X:AxisSource.None;p.LeftY=sources.Contains(AxisSource.Y)?AxisSource.Y:AxisSource.None;
        p.PovIndex=povs>0?0:-1;return p;
    }
}
