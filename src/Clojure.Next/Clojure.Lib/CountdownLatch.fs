namespace Clojure.Lib

open System.Threading

/// A count-down latch.
/// The interface that is implemented matches the one for java.util.concurrent.CountDownLatch.
type CountdownLatch(count: int) =

    let mutable _count = count

    do
        if _count < 0 then
            raise (System.ArgumentException("Count must be non-negative."))

    // The C# version has this in the constructor:
    //    lock (_synch)
    //    {
    //        _count = count;
    //    }
    // Is it possible for someone to access _count before we've exited the constructor?

    /// An object used for synchronization.
    member val private _synch = new obj ()

    /// The current count.
    member this.Count = _count

    member this.Await() =
        lock this._synch (fun () ->
            while _count > 0 do
                Monitor.Wait(this._synch) |> ignore)

    member this.Await(timeout: int) =
        lock this._synch (fun () ->
            if _count = 0 then
                true
            else
                Monitor.Wait(this._synch, timeout) |> ignore
                _count = 0)

    member this.CountDown() =
        lock this._synch (fun () ->
            if _count > 0 then
                _count <- _count - 1

                if _count = 0 then
                    Monitor.PulseAll(this._synch))

    override this.ToString() =
        sprintf "<CountdownLatch, Count = %d>" _count
