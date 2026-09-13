using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Pame.TestGame;
static class MediaFixture
{
    public static void Run(string root)
    {
        Directory.CreateDirectory(root);string wave=Path.Combine(root,"silent.wav");using(var file=new BinaryWriter(File.Create(wave))){int size=8000*2*120;file.Write(Encoding.ASCII.GetBytes("RIFF"));file.Write(size+36);file.Write(Encoding.ASCII.GetBytes("WAVEfmt "));file.Write(16);file.Write((short)1);file.Write((short)1);file.Write(8000);file.Write(16000);file.Write((short)2);file.Write((short)16);file.Write(Encoding.ASCII.GetBytes("data"));file.Write(size);file.Write(new byte[size]);}
        var app=new Application();var window=new Window{Title="Pame native media fixture",Width=480,Height=180,ShowActivated=false,ShowInTaskbar=false,WindowState=WindowState.Minimized};
        using var player=new MediaPlayer{Volume=0,IsLoopingEnabled=true,Source=MediaSource.CreateFromUri(new Uri(wave))};player.CommandManager.IsEnabled=false;
        var transport=player.SystemMediaTransportControls;transport.IsEnabled=true;transport.IsPlayEnabled=true;transport.IsPauseEnabled=true;transport.IsNextEnabled=true;transport.IsPreviousEnabled=true;
        int track=1;
        void Update(){transport.DisplayUpdater.Type=MediaPlaybackType.Music;transport.DisplayUpdater.MusicProperties.Title="Pame native fixture · track "+track;transport.DisplayUpdater.MusicProperties.Artist="Windows media validation";transport.DisplayUpdater.Update();}
        void Record(string action)=>File.AppendAllText(Path.Combine(root,"commands.txt"),action+Environment.NewLine);
        transport.ButtonPressed+=(_,e)=>app.Dispatcher.BeginInvoke(()=>{Record(e.Button.ToString());switch(e.Button){case SystemMediaTransportControlsButton.Pause:player.Pause();transport.PlaybackStatus=MediaPlaybackStatus.Paused;break;case SystemMediaTransportControlsButton.Play:player.Play();transport.PlaybackStatus=MediaPlaybackStatus.Playing;break;case SystemMediaTransportControlsButton.Next:track++;Update();break;case SystemMediaTransportControlsButton.Previous:track=Math.Max(1,track-1);Update();break;}});
        transport.PlaybackPositionChangeRequested+=(_,e)=>app.Dispatcher.BeginInvoke(()=>{player.PlaybackSession.Position=e.RequestedPlaybackPosition;Record("Seek");});
        var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};var start=DateTime.UtcNow;timer.Tick+=(_,_)=>{transport.UpdateTimelineProperties(new(){StartTime=TimeSpan.Zero,EndTime=TimeSpan.FromSeconds(120),MinSeekTime=TimeSpan.Zero,MaxSeekTime=TimeSpan.FromSeconds(120),Position=player.PlaybackSession.Position});if(DateTime.UtcNow-start>TimeSpan.FromSeconds(150))window.Close();};timer.Start();Update();player.Play();transport.PlaybackStatus=MediaPlaybackStatus.Playing;app.Run(window);transport.IsEnabled=false;
    }
}
