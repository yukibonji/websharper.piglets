// $begin{copyright}
//
// This file is part of WebSharper
//
// Copyright (c) 2008-2013 IntelliFactory
//
// For open source use, WebSharper is licensed under GNU Affero General Public
// License v3.0 (AGPLv3) with open-source exceptions for most OSS license types
// (see http://websharper.com/licensing). This enables you to develop open
// source WebSharper applications royalty-free, without requiring a license.
// However, for closed source use, you must acquire a developer license.
//
// Please contact IntelliFactory for licensing and support options at
// {licensing|sales @ intellifactory.com}.
//
// $end{copyright}

namespace WebSharper.Piglets

open System
open IntelliFactory.Reactive

type ErrorSourceId = int

[<Sealed>]
[<Class>]
type ErrorMessage =
    member Message : string
    member Source : ErrorSourceId

type Result<'a> =
    | Success of 'a
    | Failure of ErrorMessage list

    static member Failwith : string -> Result<'a>
    member isSuccess : bool
    static member Map : ('a -> 'b) -> Result<'a> -> Result<'b>
    static member Map2 : ('a -> 'b -> 'c) -> Result<'a> -> Result<'b> -> Result<'c>
    static member Bind : ('a -> Result<'b>) -> (Result<'a> -> Result<'b>)

[<AbstractClass>]
type Reader<'a> =
    abstract member Latest : Result<'a>
    abstract member Subscribe : (Result<'a> -> unit) -> IDisposable
    member Id : ErrorSourceId
    [<Obsolete "Use Subscribe.">]
    member SubscribeImmediate : (Result<'a> -> unit) -> IDisposable
    member Through : Reader<'b> -> Reader<'a>
    static member Map : ('b -> 'a) -> Reader<'b> -> Reader<'a>
    static member Map2 : ('b -> 'c -> 'a) -> Reader<'b> -> Reader<'c> -> Reader<'a>
    static member MapToResult : ('b -> Result<'a>) -> Reader<'b> -> Reader<'a>
    static member MapResult : (Result<'b> -> Result<'a>) -> Reader<'b> -> Reader<'a>
    static member MapResult2 : (Result<'b> -> Result<'c> -> Result<'a>) -> Reader<'b> -> Reader<'c> -> Reader<'a>
    static member Const : 'a -> Reader<'a>
    static member ConstResult : Result<'a> -> Reader<'a>

type ErrorMessage with
    /// Create an error message associated with the given reader.
    static member Create : string -> Reader<'a> -> ErrorMessage

[<Interface>]
type Writer<'a> =
    abstract member Trigger : Result<'a> -> unit

[<Sealed>]
[<Class>]
type Stream<'a> =
    interface Writer<'a>
    inherit Reader<'a>
    override Latest : Result<'a>
    override Subscribe : (Result<'a> -> unit) -> IDisposable
    member Trigger : Result<'a> -> unit
    /// Return a new Writer that sends x to this when triggered.
    member Write : x: 'a -> Writer<unit>
    new : init: Result<'a> * ?id: ErrorSourceId -> Stream<'a>
    new : HotStream<Result<'a>> * ?id: ErrorSourceId -> Stream<'a>

[<Sealed>]
[<Class>]
type Submitter<'a> =
    interface Writer<unit>
    inherit Reader<'a>
    member Input : Reader<'a>
    member Trigger : unit -> unit

[<Sealed>]
type Piglet<'a, 'v> =
    /// Retrieve the stream associated with a Piglet.
    member Stream : Stream<'a>

[<AutoOpen>]
module Pervasives =

    val private (<<^) : ('a -> 'b -> 'c) -> 'b -> ('a -> 'c)
    val private (>>^) : ('a -> 'b) -> 'a -> ('b -> 'c) -> 'c

    /// Apply a Piglet function to a Piglet value.
    val (<*>) : Piglet<'a -> 'b, 'c -> 'd> -> Piglet<'a, 'd -> 'e> -> Piglet<'b, 'c -> 'e>

    /// Apply a Piglet function to a Piglet Result.
    val (<*?>) : Piglet<'a -> 'b, 'c -> 'd> -> Piglet<Result<'a>, 'd -> 'e> -> Piglet<'b, 'c -> 'e>

    type Writer<'a> with
        static member Wrap : ('b -> 'a) -> Writer<'a> -> Writer<'b>
        static member WrapToResult : ('b -> Result<'a>) -> Writer<'a> -> Writer<'b>
        static member WrapResult : (Result<'b> -> Result<'a>) -> Writer<'a> -> Writer<'b>
        static member WrapAsync : ('b -> Async<'a>) -> Writer<'a> -> Writer<'b>
        static member WrapToAsyncResult : ('b -> Async<Result<'a>>) -> Writer<'a> -> Writer<'b>
        static member WrapAsyncResult : (Result<'b> -> Async<Result<'a>>) -> Writer<'a> -> Writer<'b>

module Stream =

    val Map : ('a -> 'b) -> ('b -> 'a) -> Stream<'a> -> Stream<'b>

type Container<'``in``, 'out> =
    abstract member Add : '``in`` -> unit
    abstract member Remove : int -> unit
    abstract member MoveUp : int -> unit
    abstract member Container : 'out

module Many =

    [<Class>]
    type Operations =
        member Delete : Writer<unit>
        member MoveUp : Submitter<unit>
        member MoveDown : Submitter<unit>

    [<Class>]
    type Stream<'a, 'v, 'w,'y,'z> =
        inherit Reader<'a[]>

        ///Render the element collection inside this Piglet inside the given container and with the provided rendering function
        member Render : Container<'w, 'u> -> (Operations -> 'v) -> 'u

        ///Stream where new elements for the collection are written
        member Add : Writer<'a>

        ///Function that provides the Adder Piglet with a rendering function
        member AddRender : 'y -> 'z

    [<Class>]
    type UnitStream<'a, 'v, 'w> =
        inherit Stream<'a,'v,'w,'v,'w>

        ///Add an element to the collection set to the default values
        member Add : Writer<unit>

module Choose =

    [<Class>]
    type Stream<'o, 'i, 'u, 'v, 'w, 'x when 'i : equality> =
        inherit Reader<'o>

        interface IDisposable

        /// Render the Piglet that allows the user to choose between different options.
        member Chooser : 'u -> 'v

        /// Get the stream of the Piglet that allows the user to choose between different options.
        member ChooserStream : Stream<'i>

        /// Render the Piglet that allows the user to choose the value for the selected option.
        member Choice : Container<'x, 'y> -> 'w -> 'y

module Piglet =

    /// Create a Piglet from a stream and a view.
    val Create : Stream<'a> -> 'v -> Piglet<'a, 'v>

    /// Create a Piglet initialized with x that passes its stream to the view.
    val Yield : 'a -> Piglet<'a, (Stream<'a> -> 'b) -> 'b>

    /// Create a Piglet initialized with failure that passes its stream to the view.
    val YieldFailure : unit -> Piglet<'a, (Stream<'a> -> 'b) -> 'b>

    /// Create a Piglet with optional value initialized with init that passes its stream to the view.
    /// The stream passed is a non-optional stream, and the given noneValue is mapped to None.
    val YieldOption : init: 'a option -> noneValue: 'a -> Piglet<'a option, (Stream<'a> -> 'b) -> 'b> when 'a : equality

    /// Create a Piglet initialized with x that doesn't pass any stream to the view.
    val Return : 'a -> Piglet<'a, 'b -> 'b>

    /// Create a Piglet initialized with failure that doesn't pass any stream to the view.
    val ReturnFailure : unit -> Piglet<'a, 'b -> 'b>

    ///Piglet that returns many values with an additional piglet used to create new values in the collection
    val ManyPiglet : 'a[] -> (Piglet<'a,'y->'z>) -> ('a -> Piglet<'a, 'v -> 'w>) -> Piglet<'a[], (Many.Stream<'a, 'v, 'w,'y,'z> -> 'x) -> 'x>

    /// Create a Piglet that returns many values, each created according to the given Piglet.
    val Many : 'a -> ('a -> Piglet<'a, 'v -> 'w>) -> Piglet<'a[], (Many.UnitStream<'a, 'v, 'w> -> 'x) -> 'x>

    /// Create a Piglet that returns many values, each created according to the given Piglet.
    val ManyInit : 'a[] -> 'a -> ('a -> Piglet<'a, 'v -> 'w>) -> Piglet<'a[], (Many.UnitStream<'a, 'v, 'w> -> 'x) -> 'x>

    /// Create a Piglet that allows the user to choose between several options.
    val Choose : Piglet<'i, 'u -> 'v> -> ('i -> Piglet<'o, 'w -> 'x>) -> Piglet<'o, (Choose.Stream<'o, 'i, 'u, 'v, 'w, 'x> -> 'y) -> 'y>

    /// Create a Piglet value that streams the value every time it receives a signal.
    /// The signaling function is passed to the view.
    val WithSubmit : Piglet<'a, 'b -> Submitter<'a> -> 'c> -> Piglet<'a, 'b -> 'c>

    /// Create a Piglet value that streams the value every time it receives a signal.
    /// The signaling function is passed to the view.
    /// Any update to the input Piglet passes `Failure []` to the output.
    /// This is useful to clear error messages from a previous submission.
    val WithSubmitClearError : Piglet<'a, 'b -> Submitter<'a> -> 'c> -> Piglet<'a, 'b -> 'c>

    /// Pass this Piglet's stream to the view.
    val TransmitStream : Piglet<'a, 'b -> Stream<'a> -> 'c> -> Piglet<'a, 'b -> 'c>

    /// Pass a reader for this Piglet's stream to the view.
    val TransmitReader : Piglet<'a, 'b -> Reader<'a> -> 'c> -> Piglet<'a, 'b -> 'c>

    /// Pass a mapped reader for this Piglet's stream to the view.
    val TransmitReaderMap : ('a -> 'd) -> Piglet<'a, 'b -> Reader<'d> -> 'c> -> Piglet<'a, 'b -> 'c>

    /// Pass a mapped reader for this Piglet's stream to the view.
    val TransmitReaderMapResult : (Result<'a> -> Result<'d>) -> Piglet<'a, 'b -> Reader<'d> -> 'c> -> Piglet<'a, 'b -> 'c>

    /// Pass a mapped reader for this Piglet's stream to the view.
    val TransmitReaderMapToResult : ('a -> Result<'d>) -> Piglet<'a, 'b -> Reader<'d> -> 'c> -> Piglet<'a, 'b -> 'c>

    /// Pass a writer for this Piglet's stream to the view.
    val TransmitWriter : Piglet<'a, 'b -> Writer<'a> -> 'c> -> Piglet<'a, 'b -> 'c>

    /// Map the value of a Piglet, without changing its view.
    val Map : ('a -> 'b) -> Piglet<'a, 'v> -> Piglet<'b, 'v>

    /// Map the value of a Piglet, without changing its view.
    val MapToResult : ('a -> Result<'b>) -> Piglet<'a, 'v> -> Piglet<'b, 'v>

    /// Map the Result of a Piglet, without changing its view.
    val MapResult : (Result<'a> -> Result<'b>) -> Piglet<'a, 'v> -> Piglet<'b, 'v>

    /// Map the value of a Piglet, without changing its view.
    val MapAsync : ('a -> Async<'b>) -> Piglet<'a, 'v> -> Piglet<'b, 'v>

    /// Map the value of a Piglet, without changing its view.
    val MapToAsyncResult : ('a -> Async<Result<'b>>) -> Piglet<'a, 'v> -> Piglet<'b, 'v>

    /// Map the Result of a Piglet, without changing its view.
    val MapAsyncResult : (Result<'a> -> Async<Result<'b>>) -> Piglet<'a, 'v> -> Piglet<'b, 'v>

    /// Map the value of a Piglet, without changing its view.
    /// The function can write directly into the output, zero, one or many times.
    val MapWithWriter : (Writer<'b> -> 'a -> unit) -> Piglet<'a, 'v> -> Piglet<'b, 'v>

    /// Map the Result of a Piglet, without changing its view.
    /// The function can write directly into the output, zero, one or many times.
    val MapResultWithWriter : (Writer<'b> -> Result<'a> -> unit) -> Piglet<'a, 'v> -> Piglet<'b, 'v>

    /// Flush error messages, replacing any failing state with a message-less failing state.
    val FlushErrors : Piglet<'a, 'v> -> Piglet<'a, 'v>

    /// Run the action every time the Piglet's stream receives successful data.
    val Run : action: ('a -> unit) -> Piglet<'a, 'b> -> Piglet<'a, 'b>

    /// Run the action every time the Piglet's stream receives data.
    val RunResult : action: (Result<'a> -> unit) -> Piglet<'a, 'b> -> Piglet<'a, 'b>

    /// Run a Piglet UI with the given view.
    val Render : 'v -> Piglet<'a, 'v -> 'elt> -> 'elt

    /// Map the arguments passed to the view.
    val MapViewArgs : 'va -> Piglet<'a, 'va -> 'vb> -> Piglet<'a, ('vb -> 'vc) -> 'vc>

    /// Create a Piglet for a double field for confirmation (e.g. for passwords).
    val Confirm : init:'a -> validate:(Piglet<'a,((Stream<'a> -> 'b) -> 'b)> -> Piglet<'a,(('c -> 'd -> 'c * 'd) -> Stream<'a> -> 'e)>) -> nomatch:string -> Piglet<'a,(('e -> 'f) -> 'f)> when 'a : equality

    type Builder =
        | Do

        member Bind : Piglet<'i, 'u -> 'v> * ('i -> Piglet<'o, 'w -> 'x>) -> Piglet<'o, (Choose.Stream<'o, 'i, 'u, 'v, 'w, 'x> -> 'y) -> 'y>

        member Return : 'a -> Piglet<'a, 'b -> 'b>

        member ReturnFrom : Piglet<'a, 'v> -> Piglet<'a, 'v>

        member Yield : 'a -> Piglet<'a, (Stream<'a> -> 'b) -> 'b>

        member YieldFrom : Piglet<'a, 'v> -> Piglet<'a, 'v>

        member Zero : unit -> Piglet<'a, 'b -> 'b>

module Validation =

    /// If the Piglet value passes the predicate, it is passed on;
    /// else, `Failwith msg` is passed on.
    val Is : pred: ('a -> bool) -> msg: string -> Piglet<'a, 'b> -> Piglet<'a, 'b>

    /// If the Piglet value passes the predicate, it is passed on;
    /// else, `Failure [msg]` is passed on.
    val Is' : pred: ('a -> bool) -> msg: ErrorMessage -> Piglet<'a, 'b> -> Piglet<'a, 'b>

    /// Checks that the given string Piglet is not empty, otherwise
    /// the given error message is passed on.
    val IsNotEmpty : msg: string -> Piglet<string, 'b> -> Piglet<string, 'b>

    /// Checks that the given string Piglet matches the given regex,
    /// otherwise the given error message is passed on.
    val IsMatch : regexp: string -> msg: string -> Piglet<string, 'b> -> Piglet<string, 'b>

    /// Checks that a string is not empty.
    /// Can be used as predicate for Is and Is', eg:
    /// Validation.Is Validation.NotEmpty "Field must not be empty."
    val NotEmpty : value: string -> bool

    /// Check that a string matches a regexp.
    /// Can be used as predicate for Is and Is', eg:
    /// Validation.Is (Validation.Match "^test.*") "Field must start with 'test'."
    val Match : regexp: string -> (string -> bool)
