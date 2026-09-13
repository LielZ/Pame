using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace Pame.App;
public partial class MainWindow
{
    Action<string>? keyboardText;
    Action? keyboardBack;
    void ShowTextKeyboard(string title,bool secret,Action<string> submit,string initial="",string submitLabel="Connect",int maxLength=128)
    {
        string value=initial;bool upper=false,symbols=false;TextBlock? preview=null;
        void Update(){if(preview!=null){string display=secret?new string('•',value.Length):value;preview.Text=value.Length==0?"Enter using your controller…":display.Length>100?"…"+display[^100..]:display;}}
        void Append(string text){if(value.Length+text.Length<=maxLength)value+=text;Update();}
        void Delete(){if(value.Length>0)value=value[..^1];Update();}
        void Draw()
        {
            modalButtons.Clear();var body=new StackPanel();preview=Text("",26,Colors.White);preview.TextWrapping=TextWrapping.Wrap;var entry=Panel(preview,784);entry.Padding=new(20);entry.Margin=new(0,0,0,18);body.Children.Add(entry);Update();
            var keys=new WrapPanel{Width=790};var alphabet=symbols?"1234567890!@#$%^&*()-_=+[]{};:'\",.<>/?\\|`~":upper?"QWERTYUIOPASDFGHJKLZXCVBNM1234567890":"qwertyuiopasdfghjklzxcvbnm1234567890";
            foreach(char letter in alphabet){char capture=letter;var key=Button(letter.ToString(),"textkey:"+letter,()=>Append(capture.ToString()),true);key.Width=68;key.Height=51;key.Padding=new(0);key.Margin=new(0,0,9,9);keys.Children.Add(key);}body.Children.Add(keys);
            var actions=new WrapPanel();foreach(var (label,action) in new (string,Action)[]{(upper?"Lowercase":"Uppercase",()=>{upper=!upper;symbols=false;Draw();}),(symbols?"Letters":"Symbols",()=>{symbols=!symbols;Draw();}),("Space",()=>Append(" ")),("Delete",Delete),("Clear",()=>{value="";Update();}),(submitLabel,()=>{var input=value;value="";HideModal();submit(input);}),("Cancel",()=>{value="";HideModal();})}){var b=Button(label,"textaction:"+label,action,true);b.Margin=new(0,7,10,0);b.FontSize=17;actions.Children.Add(b);}body.Children.Add(actions);
            ShowDialog(title,body,secret?"Your password stays hidden while you type. Pame does not save keyboard entries in its database or logs.":"Enter text with the on-screen keyboard.",885);keyboardText=Append;keyboardBack=Delete;
        }
        Draw();
    }
}
