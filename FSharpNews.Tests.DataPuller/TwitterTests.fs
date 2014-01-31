﻿module FSharpNews.Tests.DataPuller.TwitterTests

open System
open System.Threading
open NUnit.Framework
open Suave.Types
open Suave.Socket
open Suave.Http
open Suave.Web
open FSharpNews.Data
open FSharpNews.Utils
open FSharpNews.Tests.Core

let writeTweet json (req: HttpRequest) =
    async { do! TwitterApi.writeHeaderBodyDelimeter req
            do! TwitterApi.writeMessage req json
            do! TwitterApi.writeEmptyInfinite req }

[<SetUp>]
let Setup() = do Storage.deleteAll()

[<Test>]
let ``One tweet in stream => one activity in storage``() =
    do TwitterApi.runServer (POST >>= url TwitterApi.path >>== TwitterApi.handle (writeTweet TestData.Twitter.json))
    do StackExchangeApi.runEmpty()
    do NuGetApi.runEmpty()

    use puller = DataPullerApp.start()
    sleep 10

    let activities = Storage.getAllActivities()
    activities
    |> List.map fst
    |> Collection.assertEquiv ([ TestData.Twitter.activity ])

    activities
    |> List.map snd
    |> List.iter (fun addedTime -> Assert.That(addedTime, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(15.)), "added time"))