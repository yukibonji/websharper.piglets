﻿module IntelliFactory.WebSharper.Piglets.Controls

open IntelliFactory.WebSharper
open IntelliFactory.WebSharper.Html

[<JavaScript>]
let nextId =
    let current = ref 0
    fun () ->
        incr current
        "pl__" + string !current

[<JavaScript>]
let input ``type`` ofString toString (stream: Stream<'a>) =
    let i = Default.Input [Attr.Type ``type``]
    match stream.Latest with
    | Failure _ -> ()
    | Success x -> i.Value <- toString x
    stream.Subscribe(function
        | Success x ->
            let s = toString x
            if i.Value <> s then i.Value <- s
        | Failure _ -> ())
    let ev (_: Dom.Event) = stream.Trigger(Success (ofString i.Value))
    i.Body.AddEventListener("keyup", ev, true)
    i.Body.AddEventListener("change", ev, true)
    i

[<JavaScript>]
let WithLabel label element =
    let id = nextId()
    Span [Label [Attr.For id; Text label]; element -< [Attr.Id id]]

[<JavaScript>]
let WithLabelAfter label element =
    let id = nextId()
    Span [element -< [Attr.Id id]; Label [Attr.For id; Text label]]

[<JavaScript>]
[<Inline>]
let Input stream =
    input "text" id id stream

[<JavaScript>]
[<Inline>]
let Password stream =
    input "password" id id stream

[<JavaScript>]
let IntInput (stream: Stream<int>) =
    input "number" int string stream

[<JavaScript>]
let TextArea (stream: Stream<string>) =
    let i = Default.TextArea []
    match stream.Latest with
    | Failure _ -> ()
    | Success x -> i.Value <- x
    stream.Subscribe(function
        | Success x ->
            if i.Value <> x then i.Value <- x
        | Failure _ -> ())
    let ev (_: Dom.Event) = stream.Trigger(Success i.Value)
    i.Body.AddEventListener("keyup", ev, true)
    i.Body.AddEventListener("change", ev, true)
    i

[<JavaScript>]
let CheckBox (stream: Stream<bool>) =
    let id = nextId()
    let i = Default.Input [Attr.Type "checkbox"; Attr.Id id]
    match stream.Latest with
    | Failure _ -> ()
    | Success x -> i.Body?``checked`` <- x
    stream.Subscribe(function
        | Success x ->
            if i.Body?``checked`` <> x then i.Body?``checked`` <- x
        | Failure _ -> ())
    let ev (_: Dom.Event) = stream.Trigger(Success i.Body?``checked``)
    i.Body.AddEventListener("change", ev, true)
    i

[<JavaScript>]
let Radio (stream: Stream<'a>) (values: seq<'a * string>) =
    let name = nextId()
    let values = List.ofSeq values
    let elts = values |> List.map (fun (x, label) ->
        let id = nextId()
        let input =
            Html.Default.Input [Attr.Type "radio"; Attr.Name name; Attr.Id id]
            |>! OnChange (fun div ->
                if div.Body?``checked`` then
                    stream.Trigger(Success x))
        input, Span [
            input
            Label [Attr.For id; Text label]
        ])
    Div (Seq.map snd elts)
    |>! OnAfterRender (fun div ->
        let set = function
            | Success v ->
                (values, elts) ||> List.iter2 (fun (x, _) (input, _) ->
                    input.Body?``checked`` <- x = v)
            | Failure _ -> ()
        set stream.Latest
        stream.Subscribe set)

[<JavaScript>]
let ShowResult
        (reader: Reader<'a>)
        (render: Result<'a> -> #seq<#IPagelet>)
        (container: Element) =
    for e in render reader.Latest do
        container.Append (e :> IPagelet)
    reader.Subscribe(fun x ->
        container.Clear()
        for e in render x do
            container.Append(e :> IPagelet))
    container

[<JavaScript>]
let Show
        (reader: Reader<'a>)
        (render: 'a -> #seq<#IPagelet>)
        (container: Element) =
    let render = function
        | Success x -> render x :> seq<_>
        | Failure _ -> Seq.empty
    ShowResult reader render container

[<JavaScript>]
let ShowString reader render container =
    Show reader (fun x -> [Text (render x)]) container

[<JavaScript>]
let ShowErrors
        (reader: Reader<'a>)
        (render: string list -> #seq<#IPagelet>)
        (container: Element) =
    let render = function
        | Success (x: 'a) -> Seq.empty
        | Failure m -> render m :> seq<_>
    ShowResult reader render container

[<JavaScript>]
let Submit (submit: Writer<_>) =
    Default.Input [Attr.Type "submit"]
    |>! OnClick (fun _ _ -> (submit :> Writer<unit>).Trigger(Success()))

[<JavaScript>]
let EnableOnSuccess (reader: Reader<'a>) (element: Element) =
    element
    |>! OnAfterRender (fun el ->
        el.Body?disabled <- not reader.Latest.isSuccess
        reader.Subscribe(fun x -> el.Body?disabled <- not x.isSuccess))

[<JavaScript>]
let Button (submit: Writer<unit>) =
    Default.Input [Attr.Type "button"]
    |>! OnClick (fun _ _ -> submit.Trigger(Success()))

[<JavaScript>]
let Attr
        (reader: Reader<'a>)
        (attrName: string)
        (render: 'a -> string)
        (element: Element) =
    element
    |>! OnAfterRender (fun element ->
        let set x =
            match x with
            | Failure _ -> ()
            | Success x -> element.SetAttribute(attrName, render x)
        set reader.Latest
        reader.Subscribe set)

[<JavaScript>]
let AttrResult
        (reader: Reader<'a>)
        (attrName: string)
        (render: Result<'a> -> string)
        (element: Element) =
    element
    |>! OnAfterRender (fun element ->
        let set x = element.SetAttribute(attrName, render x)
        set reader.Latest
        reader.Subscribe set)

[<JavaScript>]
let Css
        (reader: Reader<'a>)
        (attrName: string)
        (render: 'a -> string)
        (element: Element) =
    element
    |>! OnAfterRender (fun element ->
        let set x =
            match x with
            | Failure _ -> ()
            | Success x -> element.SetCss(attrName, render x)
        set reader.Latest
        reader.Subscribe set)

[<JavaScript>]
let CssResult
        (reader: Reader<'a>)
        (attrName: string)
        (render: Result<'a> -> string)
        (element: Element) =
    element
    |>! OnAfterRender (fun element ->
        let set x = element.SetCss(attrName, render x)
        set reader.Latest
        reader.Subscribe set)
