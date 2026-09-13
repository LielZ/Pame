using System.Diagnostics;
using Pame.Core;

namespace Pame.Windows;

public sealed record SavedProcessState(int Id,long StartedUtcTicks,string Executable,int? Priority,long? Affinity)
{
    public bool Matches(int id,long start,string path)=>Id==id&&StartedUtcTicks==start&&string.Equals(Executable,path,StringComparison.OrdinalIgnoreCase);
}
public sealed class ProcessProfileService
{
    readonly Database db;
    readonly List<SavedProcessState> saved;
    public ProcessProfileService(Database db){this.db=db;saved=db.Get("recovery.processes",new List<SavedProcessState>());Restore();}
    public void Apply(Process process,GameProfile profile)
    {
        if(!profile.AboveNormalPriority&&profile.CpuAffinity==0)return;
        if(saved.Any(s=>s.Id==process.Id))return;
        try
        {
            var entry=new SavedProcessState(process.Id,process.StartTime.ToUniversalTime().Ticks,process.MainModule!.FileName,profile.AboveNormalPriority?(int)process.PriorityClass:null,profile.CpuAffinity>0?(long)process.ProcessorAffinity:null);
            saved.Add(entry);Persist();
            if(profile.AboveNormalPriority)process.PriorityClass=ProcessPriorityClass.AboveNormal;
            if(profile.CpuAffinity>0)process.ProcessorAffinity=(nint)profile.CpuAffinity;
        }catch(Exception e){Log.Error("profile.process",e);}
    }
    public void Restore()
    {
        foreach(var entry in saved.ToArray())
        {
            try
            {
                using var process=Process.GetProcessById(entry.Id);
                if(entry.Matches(process.Id,process.StartTime.ToUniversalTime().Ticks,process.MainModule?.FileName??""))
                {
                    if(entry.Priority is int priority)process.PriorityClass=(ProcessPriorityClass)priority;
                    if(entry.Affinity is long affinity)process.ProcessorAffinity=(nint)affinity;
                }
                saved.Remove(entry);
            }
            catch(ArgumentException){saved.Remove(entry);}
            catch(InvalidOperationException){saved.Remove(entry);}
            catch(Exception e){Log.Error("profile.restoreProcess",e);}
        }
        Persist();
    }
    void Persist()=>db.Set("recovery.processes",saved);
}
