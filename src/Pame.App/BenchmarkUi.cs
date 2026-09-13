using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
namespace Pame.App;
public partial class MainWindow
{
    async Task BenchmarkUi(string name)
    {
        var path=Path.Combine(dataRoot,"benchmarks");Directory.CreateDirectory(path);
        var samples=new List<object>();var frameGaps=new List<double>();TimeSpan previous=TimeSpan.Zero;
        void Rendering(object? sender,EventArgs args){var now=((RenderingEventArgs)args).RenderingTime;if(previous!=TimeSpan.Zero&&now>previous)frameGaps.Add((now-previous).TotalMilliseconds);previous=now;}
        await Task.Delay(2000);CompositionTarget.Rendering+=Rendering;var cpuStart=Process.GetCurrentProcess().TotalProcessorTime;var total=Stopwatch.StartNew();
        async Task Sample(string operation,Action action,int settle)
        {
            var sw=Stopwatch.StartNew();action();double actionMs=sw.Elapsed.TotalMilliseconds;
            await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.Render);samples.Add(new{operation,actionMs,dispatchMs=sw.Elapsed.TotalMilliseconds});await Task.Delay(settle);
        }
        try
        {
            for(int round=0;round<2;round++)foreach(var pageName in pages)await Sample("page:"+pageName,()=>Navigate(pageName),250);
            Navigate("Home");await Task.Delay(300);var cards=pageButtons.Where(b=>b.Tag?.ToString()?.StartsWith("game:")==true).Take(7).ToArray();
            for(int round=0;round<3;round++)foreach(var card in cards)await Sample("focus:"+card.Tag,()=>card.Focus(),130);
            ShowPerformance();await Task.Delay(300);HideModal();Navigate("Home");await Task.Delay(300);
            SaveVisual(design,Path.Combine(path,name+".png"),3840,2160);
        }
        finally{CompositionTarget.Rendering-=Rendering;}
        var report=new{renderer=System.Windows.Media.RenderOptions.ProcessRenderMode.ToString(),tier=RenderCapability.Tier>>16,dpi=VisualTreeHelper.GetDpi(this).PixelsPerInchX,window=new{ActualWidth,ActualHeight},seconds=total.Elapsed.TotalSeconds,cpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-cpuStart).TotalSeconds,frames=frameGaps.Count,frameGapMedian=frameGaps.Count>0?frameGaps.Order().ElementAt(frameGaps.Count/2):0,frameGapP95=frameGaps.Count>0?frameGaps.Order().ElementAt((int)(frameGaps.Count*.95)):0,samples};
        await File.WriteAllTextAsync(Path.Combine(path,name+".json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Close();
    }
}
