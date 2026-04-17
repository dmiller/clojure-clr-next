---
layout: post
title: Runtime async
date: 2026-04-16 00:00:00 -0500
categories: general
---

Starting with .NET 11, the .NET runtime now offers runtime support for async/await. The latest release of ClojureCLR (1.12.3-alpha6) provides experimental support for this feature.  In this post, we'll take a look at what async/await, how it works on the CLR, and how ClojureCLR supports it.

## The async feature

Async is feature that allows methods to suspend their computation while waiting for a subcomputation to complete.  Often, the subcomputation is something that requires waiting for an event, such as completion of an I/O operation.  Suspending the computation means yielding control of the thread of execution, which frees the current thread to be used for other purposes during the wait, thus improving multi-threading efficiency.

To take advantage of async in C#, one marks the method's signature with the `async` keyword.  Within the method's body, the `await` keyword can be used to mark the specific suspension points.   A simple example:

```C#
public static class AsyncExample
{
    public static async Task<List<string>> GetData(List<string> filenames)
    {
        List<string> contents = new List<string>(filenames.Count);
        foreach (string filename in filenames)
        {
            contents.Add(await GetDataFromFile(filename));
        }
        return contents;
    }

    private static async Task<string> GetDataFromFile(string filename)
    {
       return await System.IO.File.ReadAllTextAsync(filename);
    }  
 
}
```

(No need for the class or the methods to be `static`, but that seems correct for this example.)

Several things to note:  

- Methods marked as `async` must have a return type derived from one of these:
    - `Task`
    - `ValueTask`
    - `Task<TResult>`
    - `ValueTask<TResult>`

- There is contagion here.  We defined `GetDataFromFile` as `async`.  When we use `await` on it in the caller, the caller must be marked as `async`.  (One can use methods from the Task library to avoid using `await`, but `await` is the typical mechanism.)

## Runtime implementation

Prior to .NET 11,  `async` and `await` in C# was implemented using code transformations performed by the compiler.  A method marked `async` is rewritten as a finite-state machine.  Suspension points required mechanisms to save and restore state around tha `await` call. This consumed heap memory. Amd the resulting code caused things like stack traces in exceptions to be loaded with compiler-generated method names that were not very helpful to the programmer.  

As of .NET 11, these heroic compiler efforts are no longer required.  The CLR core runtime detects methods marked as `async` (the compiler needs to pass along that information in the method metadata).  The compiler translates `await` calls into certain special method calls that the runtime recognizes.  The runtime now has the capability of dealing with saving and restoring state on its own.   Simpler, and also more efficient, according to benchmarks.

## The story for ClojureCLR

Starting with version 1.12.3-alpha6, ClojureCLR provides experimental support for runtime async.  A new library `clojure.clr.async.task.alpha` provides functions to help with this.  Internally, the ClojureCLR compiler has been modified to pass along the critical metadata on async functions and to rewrite `await` calls to the special methods used by the runtime. 

(This library is marked "alpha" because we are still experimenting with the API.  Also, runtime async in .NET 11 is still in preview.  In the final release, the library will be in namespace `clojure.clr.async.task`.)

It is helpful to import the namespace..  From the sample file for this post, we start with:

```clojure
(ns test.async-test
  (:require [clojure.clr.async.task.alpha :as t])
  (:import [System.Threading.Tasks Task]
           [System.IO Path File]
		   [System.Threading CancellationToken]))
```

We have imported a few other classes for the example code.  `Task` is useful.  We will also find `Task<Object>` useful, so we define an alias for it.

```clojure
(alias-type ObjTask |System.Threading.Tasks.Task`1[Object]|)
```

We're going to do some file I/O in our examples, so it helpful to have a few random files to work with:

```clojure
(def ^String in-file (Path/GetTempFileName))  
(def ^String out-file (Path/GetTempFileName))

(File/WriteAllText in-file "Some random content.")
```

### Accessing a task result

If all you want to do is call an async method without awaiting it (yielding the thread), then you can use `t/result`.  `t/result` will run a task if it is not already started/completed and wait to return its result.  More typically we use it to extract the result of a task that has already completed, but it will start the task and wait if necessary.

```clojure
(t/result (File/ReadAllTextAsync in-file CancellationToken/None))  ;; => "Some random content."
```

### Async functions

If you want to use `await` to mark a suspension point where control is yielded, you need to working in an async context.  One way to provide that context is to `defn` a function with the `^:async` tag.  This marks all of its overloads (`IFn.invoke` methods) as async for the runtime.  It also type hints the return type of the function as `System.Threading.Tasks.Task<Object>`.  This applies to all the arities of the function.

```clojure
(defn ^:async shout-it-out [infile outfile]
  (let [content (t/await (File/ReadAllTextAsync infile CancellationToken/None))
        capitalized (.ToUpper content)]
    (t/await (File/WriteAllTextAsync ^String outfile capitalized CancellationToken/None))
	"I'm done yelling."))
```

We have suspension points at the read and write calls, which are asynchronous I/O operations.  The function will yield control at those points, allowing the thread to be used for other purposes while waiting for the I/O operations to complete.  When the operations complete, the function will resume execution at the point of suspension.

Note that calling `shout-it-out` will return a `Task<Object>`. 

```clojure
(def t1 (shout-it-out in-file out-file))

(t/task? t1)  ; => true
```

To get the result of the task, we can use `t/result`.  In this case, again, the `t/result` will start and wait on the task if it is not already completed.

```clojure
(t/result t1) ; => "I'm done yelling."
```

If you want to use await in a function but don't want the function itself to return a task, but just get on with things, you can use `t/async` to provide an `:async` context.  `t/async` wraps its body in an `^:async (fn [] ...body...)`.  This form returns a task, so you will need to a tas operation on it to get work done.
Typically, you will need to call `t/result` if you want to get the value from the awaited call.

```clojure
(defn just-read [infile]
   (t/result (t/async (t/await (File/ReadAllTextAsync infile CancellationToken/None)))))

(just-read in-file) ;; => "Some random content"
```

The call to `t/async` returns a task, so we need to call `t/result` to get the value from the awaited call.  If we had not done that, we would have gotten a `Task<Object>` back instead of the string content. In that case, it would be preferable generally to just define an `^:async` function if you want to use `await` in it, but `t/async` can be useful if you want an anonymous async function.  This might be useful to construct tasks for use in `wait-all` or `wait-any` calls, for example.  (See below.)

## Some utility functions

There are several utility functions to create basic tasks.

```clojure
(t/->task 42)      ;; creates a Task<Object> that returns 42 when run
(t/completed-task) ;; creates a Task that is already completed
(t/delay-task 3000) ;; creates a Task that delays for 3000 milliseconds
```

You can run any zero-arg Clojure function as a task:

```clojure
(t/result (t/run (fn [] (+ 1 2 3))))

(defn now [] DateTime/Now)
(t/result (t/run now))
```

Again, calling `t/result` on the result of `t/run` will start the task if it is not already started and wait for it to complete, returning the result.  This does not take full advantage of suspension.

## Waiting for one or for all

You can do wait-for-one and wait-for-all operations on a group of tasks.
You can either just run the tasks or ask for their value(s).

| Function | Description |
|----------|-------------|
| `(t/wait-all tasks)` |   wait for all the tasks to complete; return `nil` |
| `(t/wait-any tasks)`  |   start all tasks, return the first one to complete |
| `(t/wait-all-results tasks)` | wait for all the tasks to complete, return a lazy sequence of their results |
| `(t/wait-any-result tasks)`  | return the result of the first task to complete |

An example:

```clojure
;; A little dummy function to delay and then return a value.

(defn ^:async delayed-value [msecs val]
  (t/await (t/delay-task msecs))
  val)
  
;; Just a little test to make sure things are taking time.

(time (t/result (delayed-value 4000 7)))  ;; => take more than 4 seconds  

(t/wait-all-results [(delayed-value 2000 2000) 
                     (delayed-value 4000 4000) 
                     (delayed-value 6000 6000)]) ;; => (2000 4000 6000)
(t/wait-any-result [(delayed-value 2000 2000) 
                     (delayed-value 4000 4000) 
                     (delayed-value 6000 6000)])  ;; => 2000 (most likely)

(t/wait-all-results [(delayed-value 2000 2000) 
                     (delayed-value 4000 4000) 
                     (delayed-value 6000 6000)] 
                    500)                          ;; => nil  (times out)
(t/wait-any-result [(delayed-value 2000 2000) 
                    (delayed-value 4000 4000) 
                    (delayed-value 6000 6000)] 
                   500)                           ;; => nil  (times out)
```

## Does it work?

Short answer: yeah.

I did some simple tests to look at things like thread affinity and flooding the thread pool.  The simplest things to do is to replace a call like `(t/await ...)` with `(.Wait ...)`.  The latter does not yield its thread;  the difference in performance is notable.

## Where to use it?



