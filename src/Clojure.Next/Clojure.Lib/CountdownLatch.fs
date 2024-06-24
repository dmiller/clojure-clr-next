namespace Clojure.Lib

open System.Threading

// The interface that is implemented matches the one for java.util.concurrent.CountDownLatch.</remarks>
type CountdownLatch(count: int) =

    let mutable count = count

    do 
        if count < 0 then raise (System.ArgumentException("Count must be non-negative."))
 
    // The C# version has this in the constructor:
    //    lock (_synch)
    //    {
    //        _count = count;
    //    }
    // Is it possible for someone to access _count before we've exited the constructor?

    member val synch = new obj()

    member this.Count = count
    
    member this.Await() =
        lock this.synch (fun () ->
            while count > 0 do
                Monitor.Wait(this.synch) |> ignore)

    member this.Await(timeout: int) =
        lock this.synch (fun () ->
            if  count = 0 then
                true
            else
                Monitor.Wait(this.synch, timeout) |> ignore 
                count = 0)
               
    member this.CountDown() =
        lock this.synch (fun () ->
            if count > 0 then
                count <- count - 1
                if count = 0 then
                    Monitor.PulseAll(this.synch) )

    override this.ToString() = sprintf "<CountdownLatch, Count = %d>"  count
