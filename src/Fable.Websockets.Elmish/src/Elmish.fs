namespace Fable.Websockets.Elmish

open System
open Fable.Websockets.Protocol
open Fable.Websockets.Client

module Types =

    type SocketHandle<'serverMsg> = public {
        ConnectionId: string
        CloseHandle: ClosedCode -> string -> unit
        Sink: 'serverMsg -> unit
        mutable Subscription: IDisposable option
    } with
        override x.GetHashCode() = x.ConnectionId.GetHashCode()
        override x.Equals(b: obj) =
            match b with
            | :? SocketHandle<'serverMsg> as c -> x.ConnectionId = c.ConnectionId
            | _ -> false

    type Msg<'serverMsg, 'clientMsg, 'applicationMsg> =
            | WebsocketMsg of SocketHandle<'serverMsg> * WebsocketEvent<'clientMsg>
            | ApplicationMsg of 'applicationMsg

    [<RequireQualifiedAccess>]
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    module SocketHandle =        
        let Blackhole () : SocketHandle<'serverMsg> =
            {   Sink = ignore
                CloseHandle = fun _ _ -> ()
                ConnectionId = "empty"
                Subscription = None }

        let inline Create address (dispatcher: Elmish.Dispatch<Msg<'serverMsg,'clientMsg,'applicationMsg>>) =
            let (sink, source, closeHandle) = establishWebsocketConnection<'serverMsg,'clientMsg> address                    
            let connection =
                {   Sink = sink
                    CloseHandle = closeHandle
                    ConnectionId = Guid.NewGuid().ToString()
                    Subscription = None }
            
            let subscription = source |> Observable.subscribe (fun msg ->
                Msg.WebsocketMsg (connection, msg) |> dispatcher)

            connection.Subscription <- Some subscription
                      
module Cmd =
    open Types

    let inline public ofSocketMessage (socket: SocketHandle<'serverMsg>) (message:'serverMsg) =
        [fun (dispatcher : Elmish.Dispatch<Msg<'serverMsg,'clientMsg,'applicationMsg>>) -> socket.Sink message]
    
    let inline public tryOpenSocket address =            
        [fun (dispatcher : Elmish.Dispatch<Msg<'serverMsg,'clientMsg,'applicationMsg>>) -> SocketHandle.Create address dispatcher]

    let inline public closeSocket (socket: SocketHandle<'serverMsg>) code reason =            
        [fun (dispatcher : Elmish.Dispatch<Msg<'serverMsg,'clientMsg,'applicationMsg>>) -> do socket.CloseHandle code reason] 
