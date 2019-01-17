namespace Feblr.Browser

open System
open System.IO
open System.Diagnostics

module Downloader =
    open System.IO.Compression
    open System.Runtime.InteropServices
    open System.Security.AccessControl
    open Hopac
    open HttpFs.Client

    type Platform =
         | Linux
         | OSX
         | Windows32
         | Windows64

    let platform =
        if RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
            Linux
        elif RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
            OSX
        elif RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            if Environment.Is64BitOperatingSystem then
                Windows64
            else
                Windows32
        else
            Linux

    let defaultDownloadHost = "https://storage.googleapis.com"
    let defaultRevision = 609904

    type DownloadOption =
        { revision: int
          platform: Platform
          downloadHost: string
          downloadFolder: string
          outputFolder: string }

    let defaultDownloadOptioon =
        { revision = defaultRevision
          platform = platform
          downloadHost = defaultDownloadHost
          downloadFolder = "."
          outputFolder = "." }

    let downloadURL (option: DownloadOption) =
        match option.platform with
        | Linux ->
            sprintf "%s/chromium-browser-snapshots/Linux_x64/%d/chrome-linux.zip" option.downloadHost option.revision
        | OSX ->
            sprintf "%s/chromium-browser-snapshots/Mac/%d/chrome-mac.zip" option.downloadHost option.revision
        | Windows32 ->
            sprintf "%s/chromium-browser-snapshots/Win/%d/chrome-win.zip" option.downloadHost option.revision
        | Windows64 ->
            sprintf "%s/chromium-browser-snapshots/Win_x64/%d/chrome-win.zip" option.downloadHost option.revision

    let download (option: DownloadOption) = job {
        printfn "create download directory"
        let outputDir = Directory.CreateDirectory option.downloadFolder

        let url = downloadURL option
        printfn "download chromium from : %s" url
        use! resp = Request.createUrl Get url |> getResponse
        let zipFileName = "chromium.zip"
        let zipFilePath = Path.Combine (option.downloadFolder, zipFileName)
        use fileStream = new FileStream(zipFilePath, FileMode.Create)
        do! Job.awaitUnitTask (resp.body.CopyToAsync fileStream)
        printfn "downloaded chromium"

        printfn "start to unzip file"
        ZipFile.ExtractToDirectory (zipFilePath, option.outputFolder)
        printfn "finish unzip file"
    }

module DevToolsProtocol =
    type LaunchOption =
        { execPath: string
          arguments: string list }

    // copied from https://github.com/GoogleChrome/puppeteer/blob/master/lib/Launcher.js#L37
    let defaultArgs = [
      "--disable-background-networking"
      "--enable-features=NetworkServiceNetworkServiceInProcess"
      "--disable-background-timer-throttling"
      "--disable-backgrounding-occluded-windows"
      "--disable-breakpad"
      "--disable-client-side-phishing-detection"
      "--disable-default-apps"
      "--disable-dev-shm-usage"
      "--disable-extensions"
      "--disable-features=site-per-process"
      "--disable-hang-monitor"
      "--disable-ipc-flooding-protection"
      "--disable-popup-blocking"
      "--disable-prompt-on-repost"
      "--disable-renderer-backgrounding"
      "--disable-sync"
      "--disable-translate"
      "--force-color-profile=srgb"
      "--metrics-recording-only"
      "--no-first-run"
      "--safebrowsing-disable-auto-update"
      "--enable-automation"
      "--password-store=basic"
      "--use-mock-keychain"
    ];

    let launch (option: LaunchOption) =
        let userDataDir = Path.Combine (Path.GetTempPath(), "chronium-user-data")
        let userDataDirArg = sprintf "--user-data-dir=%s" userDataDir

        let debugPortArg = sprintf "--remote-debugging-port=0"

        let arguments =
            option.arguments
            |> List.append defaultArgs
            |> List.append [userDataDirArg; debugPortArg]
        
        let startInfo = new ProcessStartInfo()
        startInfo.FileName <- option.execPath
        startInfo.UseShellExecute <- false
        let proc = new Process()
        proc.StartInfo <- startInfo
        proc.Start()
        proc.WaitForExit()

        ignore