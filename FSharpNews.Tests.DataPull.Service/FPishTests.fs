﻿module FSharpNews.Tests.DataPull.Service.FPishTests

open System
open NUnit.Framework
open Suave.Types
open Suave.Http
open FSharpNews.Data
open FSharpNews.Utils
open FSharpNews.Tests.Core

[<SetUp>]
let Setup() = do Storage.deleteAll()

[<Test>]
let ``One question returned by api => one activity in storage``() =
    use fs = FPishApi.runServer (GET >>= url FPishApi.path >>= OK TestData.FPish.xml)
    do ServiceApplication.startAndSleep FPish

    Storage.getAllActivities()
    |> List.map fst
    |> List.exactlyOne
    |> function FPishQuestion q -> q | x -> failwithf "Expected FPishQuestion, but was %O" (x.GetType())
    |> assertEqual TestData.FPish.question
