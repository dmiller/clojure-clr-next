namespace Clojure.Collections

open System
open System.Text.RegularExpressions

/// Shim class to provide java.util.regex.Matcher capabilities for the re-* functions in core.clj.
type JReMatcher(regex: Regex, target: string) =

    let mutable _regex: Regex = regex
    let mutable _target: string = target
    let mutable _matcher: Match = null

    member _.isUnrealized = not <| isNull _regex
    member _.isFailed = isNull _regex && isNull _matcher
    member _.isUnrealizedOrFailed = not <| isNull _regex || isNull _matcher

    // Careful analysis of the re-* methods in core.clj reveal that exactly these are needed.

    member _.find() =
        let nextMatch =
            if not <| isNull _matcher then
                _matcher.NextMatch()
            elif not <| isNull _regex then
                let nextMatch = _regex.Match(_target)
                _regex <- null
                _target <- null
                nextMatch
            else
                null

        if isNull nextMatch then
            false
        elif nextMatch.Success then
            _matcher <- nextMatch
            true
        else
            _matcher <- null
            false

    // I don't implement the full functionality.
    // This needs to be called on the first attempt to make a match
    //  because we have to rewrite the regex pattern to match the whole string
    member this.matches() =
        if isNull _regex then
            false
        else
            let pattern = _regex.ToString()
            let needFront = pattern.Length = 0 || pattern.[0] <> '^'
            let needRear = pattern.Length = 0 || pattern.[pattern.Length - 1] <> '$'

            if needFront || needRear then
                let pattern =
                    (if needFront then "^" else "") + pattern + (if needRear then "$" else "")

                _regex <- new Regex(pattern)

            this.find ()

    member _.groupCount() =
        if isNull _matcher then
            raise
            <| InvalidOperationException("Attempt to call groupCount on a non-realized or failed match.")
        else
            _matcher.Groups.Count - 1

    member _.group() =
        if isNull _matcher then
            raise
            <| InvalidOperationException("Attempt to call group on a non-realized or failed match.")
        else
            _matcher.Value

    member _.group(groupIndex) =
        if isNull _matcher then
            raise
            <| InvalidOperationException("Attempt to call group on a non-realized or failed match.")

        if groupIndex < 0 || groupIndex >= _matcher.Groups.Count then
            raise
            <| ArgumentOutOfRangeException("groupIndex", "Attempt to call group with an index out of bounds.")

        _matcher.Groups.[groupIndex].Value

    member _.start = _matcher.Index
    member _.``end`` = _matcher.Index + _matcher.Length //  end is a reserved word in F#
