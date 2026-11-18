// SPDX-License-Identifier: AGPL-3.0-only

using System.IO;
using System.Windows;
using GameTranslator.App.Services;
using GameTranslator.RemotePlay;
using Microsoft.Web.WebView2.Core;

namespace GameTranslator.App.Views;

/// <summary>Sony'nin giriş sayfasını açar; giriş bitince yönlendirme adresini yakalar. Parola yalnız Sony'ye gider.</summary>
public partial class PsnLoginWindow : Window
{
    private Uri? redirect;private bool closed;private readonly string state=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    private PsnLoginWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => {try{await OpenAsync();}catch{if(!closed)LoginStatus.Text="تعذر فتح صفحة Sony الآمنة؛ أغلقها وأعد المحاولة.";}};
        Closed+=(_,_)=>{closed=true;try{Browser.Dispose();}catch{}};
    }

    /// <summary>Giriş tamamlanırsa yönlendirme adresini, vazgeçilirse null döner.</summary>
    public static Uri? Show(Window? owner)
    {
        var window = new PsnLoginWindow { Owner = owner };
        window.ShowDialog();
        return window.redirect;
    }

    // Tarayıcı süreci kapanana kadar ortam canlı kalmalı; yoksa BrowserProcessExited gelmez.
    private static CoreWebView2Environment? lastEnvironment;

    private async Task OpenAsync()
    {
        // Her girişte temiz oturum: önceki çerezler (ör. kendi hesabınız) karışmasın.
        var root = Path.Combine(AppPaths.Data, "webview");
        DeleteLeftovers(root);
        var folder = Path.Combine(root, Guid.NewGuid().ToString("N"));
        var environment = await CoreWebView2Environment.CreateAsync(null, folder);
        lastEnvironment = environment;
        // WebView2 kapanırken dosyalarını (ör. BrowserMetrics-*.pma) bir süre açık tutar. Klasör ancak tarayıcı
        // süreci bitince silinebilir; silinemeyen klasörü bir sonraki giriş temizler.
        environment.BrowserProcessExited += (_, _) => TryDelete(folder);
        if(closed)return;await Browser.EnsureCoreWebView2Async(environment);if(closed)return;
        Browser.CoreWebView2.NavigationStarting += OnNavigation;
        Browser.CoreWebView2.Navigate(PsnAccount.AuthorizeWithState(state).AbsoluteUri);
        Browser.CoreWebView2.Settings.AreDevToolsEnabled=false;
    }

    private static void DeleteLeftovers(string root)
    {
        if (!Directory.Exists(root)) return;
        foreach (var folder in Directory.EnumerateDirectories(root)) TryDelete(folder);
    }

    private static void TryDelete(string folder)
    {
        try { Directory.Delete(folder, recursive: true); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }

    private void OnNavigation(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out var candidate)){e.Cancel=true;return;}
        if(!PsnAccount.IsRedirect(candidate)){if(!PsnAccount.IsSonyPage(candidate)){e.Cancel=true;LoginStatus.Text="تم إيقاف انتقال خارج صفحات Sony الآمنة.";}return;}
        e.Cancel = true;
        if(!PsnAccount.MatchesState(candidate,state)){LoginStatus.Text="استجابة Sony لا تطابق جلسة الدخول؛ أغلق النافذة وأعد المحاولة.";return;}
        redirect = candidate;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
