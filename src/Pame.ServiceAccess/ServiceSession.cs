using System.Management;
using System.Text.Json;
using Pame.Core;

namespace Pame.ServiceAccess;

sealed class ServiceSession
{
    readonly HashSet<string> changed=new(StringComparer.Ordinal);
    readonly string journal;
    public ServiceSession(string path)
    {
        journal=path;
        if(File.Exists(path)){
            if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Recovery file cannot be a link.");
            changed.UnionWith((JsonSerializer.Deserialize<string[]>(File.ReadAllText(path))??[]).Where(BackgroundCatalog.IsService));
        }
    }
    public async Task<List<BackgroundResult>> Begin(string[] names,CancellationToken ct)
    {
        var results=new List<BackgroundResult>();
        foreach(var name in names.Distinct())
        {
            ct.ThrowIfCancellationRequested();
            if(changed.Contains(name)){results.Add(new(name,"Skipped","Recovery is pending."));continue;}
            try{
                using var service=ServiceHandle.Open(name);
                if(service.State!=4){results.Add(new(name,"Unchanged","Already stopped or not in a stable running state."));continue;}
                if(!service.CanStop||service.HasDependents){results.Add(new(name,"Skipped","Cannot stop or has active dependents."));continue;}
                if(name=="Spooler"&&!PrintingIdle()){results.Add(new(name,"Skipped","The print queue is busy or unavailable."));continue;}
                changed.Add(name);Save();await service.Stop();results.Add(new(name,"Stopped","Will restart after the game."));
            }catch(Exception error){results.Add(new(name,"Failed",error.Message));}
        }
        return results;
    }
    public async Task<List<BackgroundResult>> Restore()
    {
        var results=new List<BackgroundResult>();
        foreach(string name in changed.ToArray())try{
            using var service=ServiceHandle.Open(name);await service.Start();changed.Remove(name);Save();results.Add(new(name,"Restored","Running."));
        }catch(Exception error){results.Add(new(name,"Failed","Restore: "+error.Message));}
        return results;
    }
    void Save()
    {
        if(File.Exists(journal)&&(File.GetAttributes(journal)&FileAttributes.ReparsePoint)!=0)throw new IOException("Invalid recovery file.");
        using(var file=new FileStream(journal+".tmp",FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(file,changed);file.Flush(true);}
        File.Move(journal+".tmp",journal,true);
    }
    static bool PrintingIdle(){try{using var query=new ManagementObjectSearcher("SELECT JobId FROM Win32_PrintJob");using var jobs=query.Get();return jobs.Count==0;}catch{return false;}}
}
