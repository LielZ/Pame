using Microsoft.Win32;
using Pame.Core;

namespace Pame.Windows;

public sealed class ProfileJournal
{
    public string? AudioOutput {get;set;}
    public DisplayMode? Display {get;set;}
    public string? GpuExecutable {get;set;}
    public string? GpuValue {get;set;}
    public bool GpuExisted {get;set;}
}
public sealed class RuntimeProfileService
{
    const string GpuKey=@"Software\Microsoft\DirectX\UserGpuPreferences";
    readonly Database db;
    public RuntimeProfileService(Database db){this.db=db;Recover();}
    public void Begin(Game game,GameProfile profile)
    {
        Recover();var pending=db.Get("recovery.profile",new ProfileJournal());
        if(pending.AudioOutput!=null||pending.Display!=null||pending.GpuExecutable!=null){Log.Write("profile.previousRecoveryPending");return;}
        var journal=new ProfileJournal();
        if(!string.IsNullOrEmpty(profile.AudioOutputId))
        {
            try{using var audio=new AudioService();if(audio.Outputs().Any(d=>d.Id==profile.AudioOutputId)){journal.AudioOutput=audio.DefaultId;Save(journal);audio.SetOutput(profile.AudioOutputId);}}catch(Exception e){Log.Error("profile.audio",e);}
        }
        if(profile.RefreshRate>0&&DisplayService.Current is {} current&&current.RefreshRate!=profile.RefreshRate)
        {
            try{var requested=current with{RefreshRate=profile.RefreshRate};DisplayService.Apply(requested,true);journal.Display=current;Save(journal);DisplayService.Apply(requested);}catch(Exception e){Log.Error("profile.display",e);}
        }
        var executable=File.Exists(game.Executable)?game.Executable:game.DetectedExecutable;
        if(profile.GpuPreference is "High performance" or "Power saving"&&File.Exists(executable)&&SafetyPolicy.IsWithin(executable,game.InstallPath))
        {
            try
            {
                using var key=Registry.CurrentUser.CreateSubKey(GpuKey);journal.GpuExecutable=Path.GetFullPath(executable);journal.GpuExisted=key.GetValueNames().Contains(journal.GpuExecutable,StringComparer.OrdinalIgnoreCase);journal.GpuValue=key.GetValue(journal.GpuExecutable) as string;Save(journal);
                var preference=profile.GpuPreference=="High performance"?"2":"1";
                var other=(journal.GpuValue??"").Split(';',StringSplitOptions.RemoveEmptyEntries).Where(s=>!s.StartsWith("GpuPreference=",StringComparison.OrdinalIgnoreCase));key.SetValue(journal.GpuExecutable,string.Join(';',other.Append("GpuPreference="+preference))+";");
            }catch(Exception e){Log.Error("profile.gpuPreference",e);}
        }
    }
    void Save(ProfileJournal journal)=>db.Set("recovery.profile",journal);
    public void Recover()
    {
        var journal=db.Get("recovery.profile",new ProfileJournal());
        if(journal.AudioOutput!=null)try{using var audio=new AudioService();audio.SetOutput(journal.AudioOutput);journal.AudioOutput=null;Save(journal);}catch(Exception e){Log.Error("profile.restoreAudio",e);}
        if(journal.Display!=null)try{DisplayService.Apply(journal.Display);journal.Display=null;Save(journal);}catch(Exception e){Log.Error("profile.restoreDisplay",e);}
        if(journal.GpuExecutable!=null)try{using var key=Registry.CurrentUser.CreateSubKey(GpuKey);if(journal.GpuExisted)key.SetValue(journal.GpuExecutable,journal.GpuValue??"");else key.DeleteValue(journal.GpuExecutable,false);journal.GpuExecutable=null;Save(journal);}catch(Exception e){Log.Error("profile.restoreGpu",e);}
    }
}
