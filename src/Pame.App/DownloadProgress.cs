using System.IO;
using Pame.Core;
using Pame.Windows;

namespace Pame.App;

public partial class MainWindow
{
    bool checkingDownloads;
    DateTime nextDownloadCheck;
    async Task RefreshDownloadProgress()
    {
        if(checkingDownloads||scanning||closing||currentPage!="Downloads"||DateTime.UtcNow<nextDownloadCheck)return;
        checkingDownloads=true;nextDownloadCheck=DateTime.UtcNow.AddSeconds(5);
        var manifests=games.Where(g=>g.Installed&&g.Store==StoreKind.Steam&&File.Exists(g.ManifestPath)).Select(g=>(g.Id,g.ManifestPath)).ToArray();
        try
        {
            var values=await Task.Run(()=>manifests.Select(item=>
            {
                try{var state=Vdf.Parse(DiscoveryService.ReadShared(item.ManifestPath)).Child("AppState");return (item.Id,state!=null,(state?.Number("StateFlags")&2)!=0,Math.Max(0,state?.Number("BytesDownloaded")??0),Math.Max(0,state?.Number("BytesToDownload")??0));}
                catch(IOException){return (item.Id,false,false,0L,0L);}catch(FormatException){return (item.Id,false,false,0L,0L);}catch(UnauthorizedAccessException){return (item.Id,false,false,0L,0L);}
            }).ToArray());
            if(closing||scanning)return;
            bool changed=false;
            foreach(var (id,valid,pending,received,total) in values)
            {
                int index=games.FindIndex(g=>g.Id==id);if(!valid||index<0)continue;var game=games[index];
                if(game.UpdatePending==pending&&game.BytesDownloaded==received&&game.BytesToDownload==total)continue;
                games[index]=game with{UpdatePending=pending,BytesDownloaded=received,BytesToDownload=total};changed=true;
            }
            if(changed&&currentPage=="Downloads"&&modalLayer.Visibility!=System.Windows.Visibility.Visible)Render(true);
        }
        finally{checkingDownloads=false;}
    }
}
