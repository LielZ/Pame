using System.Text;
using System.Text.Json;

namespace Pame.Core;

public sealed record BackgroundResult(string Target,string State,string Detail);
public sealed record ServiceRequest(string Command,string[] Services);
public static class ServiceProtocol
{
    public const string ServiceName="PameServiceAccess";
    public const string PipeName="Pame.ServiceAccess.v1";
    public static bool Valid(ServiceRequest? request)=>request is {Services:not null}&&request.Services.Length<=5&&
        (request.Command=="restore"&&request.Services.Length==0||request.Command=="begin"&&request.Services.All(BackgroundCatalog.IsService));
    // Limit input before allocation/deserialization, including a missing newline.
    public static async Task<string?> Read(Stream stream,int limit,CancellationToken ct)
    {
        var bytes=new List<byte>();var next=new byte[1];
        while(await stream.ReadAsync(next,ct)!=0){if(next[0]==10)return Encoding.UTF8.GetString(bytes.ToArray());if(bytes.Count>=limit)throw new IOException("Message too large.");bytes.Add(next[0]);}
        return bytes.Count==0?null:throw new IOException("Incomplete message.");
    }
    public static Task Write<T>(Stream stream,T value,CancellationToken ct)=>stream.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)+"\n"),ct).AsTask();
}
