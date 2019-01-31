namespace Feblr.Crawler.Core

open System
open System.Threading.Tasks
open FSharp.Control.Tasks
open Orleankka
open Orleankka.FSharp

open Message
open Downloader
open Extractor

module Crawler =
    type ICrawler =
        inherit IActorGrain<CrawlerMessage>

    type Crawler() =
        inherit ActorGrain()

        let mutable currTask: CrawlTask option = None

        interface ICrawler

        override this.Receive(message) = task {
            match message with
            | :? CrawlerMessage as msg ->
                match msg with
                | StartCrawl crawlTask ->
                    currTask <- Some crawlTask
                    let crawler = ActorRef<CrawlerMessage>(this.Self)
                    do! this.download { uri = crawlTask.uri; crawler = crawler }
                    return none()
                | CancelCrawl coordinator ->
                    match currTask with
                    | Some crawlTask ->
                        do! coordinator <! TaskCancelled crawlTask
                    | None -> ()
                    return none()
                | DownloadFinished (uri, content) ->
                    return none()
            | _ ->
                return unhandled()
        }

        member this.download (downloadTask: DownloadTask): Task<unit> = task {
            do! Downloader.start this.System  downloadTask
        }

        member this.extract (extractTask: ExtractTask) (content: string): Task<unit> = task {
            do! Extractor.start this.System  extractTask
        }

        static member start (actorSystem: IActorSystem) (crawlTask: CrawlTask) = task {
            let crawlerId = sprintf "crawler.%s" crawlTask.uri.Host
            let crawler = ActorSystem.typedActorOf<ICrawler, CrawlerMessage>(actorSystem, crawlerId)
            do! crawler <! StartCrawl crawlTask
        }