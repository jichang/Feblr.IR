namespace Feblr.Crawler.Core

open System
open FSharp.Control.Tasks
open Orleankka
open Orleankka.FSharp

module Downloader =
    type CrawlTask =
        { uri: Uri
          depth: int }

    type Message =
        | StartCrawl of CrawlTask

    type IDownloader =
        inherit IActorGrain<Message>

    type Downloader() =
        inherit ActorGrain()
        interface IDownloader

        override this.Receive(message) = task {
            match message with
            | :? Message as msg ->
                match msg with
                | StartCrawl task ->
                    printfn "%A" task
                    return none()
            | _ ->
                return unhandled()
        }