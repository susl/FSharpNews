﻿module FSharpNews.Data.Storage

open System
open MongoDB.Bson
open MongoDB.Bson.Serialization.Attributes
open MongoDB.Driver
open MongoDB.Driver.Builders
open FSharpNews.Data
open FSharpNews.Utils

let private log = Logger.create "Storage"

type private ActivityType =
    | StackExchange = 0
    | Tweet = 1

let private client = new MongoClient("mongodb://localhost")
let private db = client.GetServer().GetDatabase("fsharpnews")
let private activities = db.GetCollection("activities")

let private i32 value = BsonInt32 value
let private i64 value = BsonInt64 value
let private str value = BsonString value
let private date (value: DateTime) = BsonDateTime value
let private el (name: string) (value: BsonValue) = BsonElement(name, value)

let private siteToBson = function Stackoverflow -> i32 0 | Programmers -> i32 1
let private bsonToSite = function 0 -> Stackoverflow | 1 -> Programmers | x -> failwithf "Unknown %d StackExchange site" x

let private mapToDocument (activity, raw) =
    let activityDoc, descriminator =
        match activity with
        | StackExchangeQuestion q -> BsonDocument [ el "questionId" (i32 q.Id)
                                                    el "site" (siteToBson q.Site)
                                                    el "title" (str q.Title)
                                                    el "url" (str q.Url)
                                                    el "creationDate" (date q.CreationDate) ]
                                     , i32 (int ActivityType.StackExchange)
        | Tweet t -> BsonDocument [ el "tweetId" (i64 t.Id)
                                    el "text" (str t.Text)
                                    el "userId" (i64 t.UserId)
                                    el "userScreenName" (str t.UserScreenName)
                                    el "creationDate" (date t.CreationDate) ]
                     , i32 (int ActivityType.Tweet)
    let rawDoc = BsonValue.Create(raw)
    BsonDocument [ el "descriminator" descriminator
                   el "activity" activityDoc
                   el "raw" rawDoc ]

let private mapFromDocument (document: BsonDocument) =
    let activityType = enum<ActivityType>(document.["descriminator"].AsInt32)
    let adoc = document.["activity"].AsBsonDocument
    match activityType with
    | ActivityType.StackExchange -> { Id = adoc.["questionId"].AsInt32
                                      Site = bsonToSite adoc.["site"].AsInt32
                                      Title = adoc.["title"].AsString
                                      Url = adoc.["url"].AsString
                                      CreationDate = adoc.["creationDate"].ToUniversalTime() } |> StackExchangeQuestion
    | ActivityType.Tweet -> { Id = adoc.["tweetId"].AsInt64
                              Text = adoc.["text"].AsString
                              UserId = adoc.["userId"].AsInt64
                              UserScreenName = adoc.["userScreenName"].AsString
                              CreationDate = adoc.["creationDate"].ToUniversalTime() } |> Tweet
    | t -> failwithf "Mapping for %A is not implemented" t

let save (activity: Activity, raw: string) =
    (activity, raw)
    |> mapToDocument
    |> activities.Insert
    |> ignore

let saveAll (activitiesWithRaws: (Activity*string) list) =
    match activitiesWithRaws with
    | [] -> ()
    | activitiesToSave -> activitiesToSave
                          |> List.map mapToDocument
                          |> activities.InsertBatch
                          |> ignore

let getTimeOfLastQuestion (site: StackExchangeSite) =
    let quest = activities.Find(Query.And([ Query.EQ("descriminator", i32 (int ActivityType.StackExchange))
                                            Query.EQ("activity.site", siteToBson site) ]))
                         .SetSortOrder(SortBy.Descending("activity.creationDate"))
                         .SetLimit(1)
                |> Seq.map mapFromDocument
                |> Seq.tryHead
    match quest with
    | Some (StackExchangeQuestion quest) -> quest.CreationDate
    | _ -> DateTime(2014, 1, 1, 0, 0, 0, DateTimeKind.Utc)