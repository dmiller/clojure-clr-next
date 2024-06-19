namespace Clojure.Collections

open System.Text.RegularExpressions
open System

// Shim class to provide java.util.regex.Matcher capabilities for the re-* functions in core.clj.

type JReMatcher(r: Regex, s: string ) = 

    let mutable regex : Regex = r
    let mutable target: string = s
    let mutable matcher : Match = null

    member _.isUnrealized = not <| isNull regex
    member _.isFailed = isNull regex && isNull matcher
    member _.isUnrealizedOrFailed = not <| isNull regex || isNull matcher

    // Careful analysis of the re-* methods in core.clj reveal that exactly these are needed.


    member _.find() =
        let nextMatch =
            if not <| isNull matcher then
                matcher.NextMatch()
            elif not <| isNull regex then
                let nextMatch = regex.Match(target)
                regex <- null
                target <- null
                nextMatch
            else
                null

        if isNull nextMatch then
            false
        elif
            nextMatch.Success then
            matcher <- nextMatch
            true
        else
            matcher <- null
            false

    // I don't implement the full functionality. 
    // This needs to be called on the first attempt to make a match
    //  because we have to rewrite the regex pattern to match the whole string
    member this.matches() = 
        if isNull regex then
                false
            else
                let pattern = regex.ToString()
                let needFront = pattern.Length = 0 || pattern.[0] <> '^'
                let needRear = pattern.Length = 0 || pattern.[pattern.Length - 1] <> '$'

                if needFront || needRear then
                    let pattern = (if needFront then "^" else "") + pattern + (if needRear then "$" else "")
                    regex <- new Regex(pattern)

                this.find()

    member _.groupCount() =
        if isNull matcher then
                raise <| InvalidOperationException("Attempt to call groupCount on a non-realized or failed match.")
            else
                matcher.Groups.Count - 1

    member _.group() =
        if isNull matcher then
                    raise <| InvalidOperationException("Attempt to call group on a non-realized or failed match.")
                else
                    matcher.Value
    
    member _.group(groupIndex) =
        if isNull matcher then
                raise <| InvalidOperationException("Attempt to call group on a non-realized or failed match.")

        if  groupIndex < 0 || groupIndex >= matcher.Groups.Count then
            raise <| ArgumentOutOfRangeException("groupIndex", "Attempt to call group with an index out of bounds.")

        matcher.Groups.[groupIndex].Value

    member _.start = matcher.Index
    member _.endN = matcher.Index + matcher.Length  // Can't call it end because that's a reserved word in F#