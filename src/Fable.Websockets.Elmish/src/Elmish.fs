namespace Fable.Websockets.Elmish

open System
open Fable.Websockets.Protocol
open Fable.Websockets.Client

module Types =
    type SocketHandle<'serverMsg> (sink: 'serverMsg -> unit, closeHandle: ClosedCode -> string -> unit) =
        member val internal ConnectionId = System.Guid.NewGuid()
        member val internal CloseHandle = closeHandle
        member val internal Sink = sink
        member val public Subscription:IDisposable option = None with get, set
        
        override x.GetHashCode() =
            x.ConnectionId.GetHashCode()

        override x.Equals(b) =
            match b with
            | :? SocketHandle<'serverMsg> as c -> x.ConnectionId = c.ConnectionId
            | _ -> false
    type Msg<'serverMsg, 'clientMsg, 'applicationMsg> =
            | WebsocketMsg of SocketHandle<'serverMsg> * WebsocketEvent<'clientMsg>
            | ApplicationMsg of 'applicationMsg

module SocketHandle =        
    open Types
    let Blackhole () : SocketHandle<'serverMsg> = 
        SocketHandle<'serverMsg>(ignore, fun _ _ -> ())

    let inline Create address (dispatcher: Elmish.Dispatch<Msg<'serverMsg,'clientMsg,'applicationMsg>>) =
        let (sink,source, closeHandle) = establishWebsocketConnection<'serverMsg,'clientMsg> address
        let connection = SocketHandle<'serverMsg> (sink, closeHandle)
        
        let subscription = source
                           |> Observable.subscribe (fun msg -> Msg.WebsocketMsg (connection,msg) |> dispatcher)

        connection.Subscription <- Some subscription
                      
module Cmd =
    open Types

    let public ofSocketMessage (socket: SocketHandle<'serverMsg>) (message:'serverMsg) =
        [fun (dispatcher : Elmish.Dispatch<Msg<'serverMsg,'clientMsg,'applicationMsg>>) -> socket.Sink message]
    
    let public tryOpenSocket address =            
        [fun (dispatcher : Elmish.Dispatch<Msg<'serverMsg,'clientMsg,'applicationMsg>>) -> SocketHandle.Create address dispatcher]

    let public closeSocket (socket: SocketHandle<'serverMsg>) code reason =            
        [fun (dispatcher : Elmish.Dispatch<Msg<'serverMsg,'clientMsg,'applicationMsg>>) -> do socket.CloseHandle code reason] 
