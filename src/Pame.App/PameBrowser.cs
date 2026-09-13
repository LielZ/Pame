using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Pame.Core;
using Pame.Windows;

namespace Pame.App;
public partial class MainWindow
{
    BrowserPane? browser;
    BrowserState? browserPreferences;
    Action<bool>? pendingBrowserConsent;
    bool browserOverGame;
    void BuildBrowser()
    {
        SetBackdrop(null);
        if(browser==null){
            browserPreferences=db.Get("browser",new BrowserState());browserPreferences.Favorites??=[];browserPreferences.Tabs??=[];
            browser=new BrowserPane(dataRoot,browserPreferences,()=>db.Set("browser",browserPreferences),
                (title,submit,value)=>{BrowserControls();ShowTextKeyboard(title,false,input=>{submit(input);EnterBrowserPointer();},value,"Go",4096);},
                AskBrowserPermission,EnterBrowserPointer,BrowserControls,BrowserTypeIntoPage,ReturnFromBrowser);
            browser.ControlsChanged+=RegisterBrowserButtons;browser.OptionsRequested+=ShowBrowserOptions;
        }
        if(browser.Parent is Panel old)old.Children.Remove(browser);
        browser.Width=1328;browser.Height=756;Place(page,browser,244,92);browser.SetVisible(true);RefreshBrowserHints();RegisterBrowserButtons();
        statusLine.Text="Pame browser";
    }
    void RegisterBrowserButtons(){if(browser==null||currentPage!="Browser")return;pageButtons.RemoveAll(b=>b.Tag?.ToString()?.StartsWith("browser:")==true);pageButtons.AddRange(browser.Buttons);}
    void RefreshBrowserHints(){if(browser!=null&&currentPage=="Browser")browser.UpdateHints(PromptFamily,browserPointer,sessions.ActiveGame!=null);}
    void OpenBrowser(string? url=null)
    {
        browserOverGame=sessions.ActiveGame!=null;overlay?.Hide();StopPointer();Show();WindowState=settings.Fullscreen?WindowState.Maximized:WindowState.Normal;
        Navigate("Browser");Activate();GameWindows.Activate(new WindowInteropHelper(this).Handle,keepTopmost:browserOverGame);if(!string.IsNullOrWhiteSpace(url))browser!.Go(url);EnterBrowserPointer();
    }
    void EnterBrowserPointer(){if(currentPage!="Browser"||modalLayer.Visibility==Visibility.Visible)return;desktop.Enabled=true;browserPointer=true;pointerPad=null;desktopHint?.Hide();browser?.SetVisible(true);browser?.FocusWeb();RefreshBrowserHints();}
    void BrowserControls(){StopPointer();RefreshBrowserHints();pageButtons.FirstOrDefault(b=>b.Tag?.ToString()=="browser:address")?.Focus();}
    async void BrowserTypeIntoPage()
    {
        if(browser?.Core==null){BrowserAddressEntry();return;}
        bool secret=false;try{secret=await browser.Core.ExecuteScriptAsync("document.activeElement?.type === 'password'")=="true";}catch{}
        BrowserControls();ShowTextKeyboard("Type into the page",secret,text=>{EnterBrowserPointer();_=browser.InsertText(text);},"","Type",4096);
    }
    void BrowserAddressEntry(){BrowserControls();ShowTextKeyboard("Search or enter an address",false,text=>{browser?.Go(text);EnterBrowserPointer();},browser?.CurrentUrl??"","Go",4096);}
    void AskBrowserPermission(string title,string message,Action<bool> respond)
    {
        pendingBrowserConsent?.Invoke(false);pendingBrowserConsent=respond;BrowserControls();
        ShowChoices(title,new (string,Action)[]{("Deny / cancel",()=>Finish(false)),("Allow once",()=>Finish(true))},message);
        void Finish(bool allow){pendingBrowserConsent=null;respond(allow);HideModal();EnterBrowserPointer();}
    }
    void ShowBrowserOptions()
    {
        BrowserControls();var options=new List<(string,Action)>{("Back to browser",()=>{HideModal();EnterBrowserPointer();}),
            ("Type into the page",BrowserTypeIntoPage),("Manage favorites",ShowBrowserFavorites),
            ("Pause media when hidden: "+(browserPreferences!.PauseWhenHidden?"On":"Off"),()=>{browser!.SetPauseWhenHidden(!browserPreferences.PauseWhenHidden);ShowBrowserOptions();})};
        if(sessions.ActiveGame!=null)options.Add(("Return to game",ReturnFromBrowser));
        ShowChoices("Pame browser",options,"Chromium through Microsoft Edge WebView2 · up to 8 tabs. Website sign-in and subscriptions stay with the provider.");
    }
    void ShowBrowserFavorites()=>ShowChoices("Manage favorites",browserPreferences!.Favorites.Select(item=>("Remove "+item.Title,(Action)(()=>{browser!.RemoveFavorite(item);ShowBrowserFavorites();}))).Append(("Back",(Action)ShowBrowserOptions)),"Use the star beside the address to save the current website.");
    void ReturnFromBrowser()
    {
        HideModal();StopPointer();browser?.SetVisible(false);browserOverGame=false;GameWindows.ReleaseTopmost(new WindowInteropHelper(this).Handle);
        if(sessions.GameProcessId!=null){WindowState=WindowState.Minimized;sessions.Resume();}else Navigate("Home");
    }
}
