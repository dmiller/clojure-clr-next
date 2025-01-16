namespace Clojure.IO

open Clojure.Collections
open System
open Clojure.Numerics


[<Sealed>]
type ReaderConditional private (_form: obj, _isSplicing : Nullable<bool>) =
    
    static let FormKeyword = Keyword.intern("form")
    static let SplicingKeyword = Keyword.intern("splicing?")

    member private _.Form = _form
    member private _.IsSplicing = _isSplicing

    static member create(form: obj, isSplicing: bool) =
        ReaderConditional(form, isSplicing)

    override this.Equals(obj) =
        match obj with
        | _ when Object.ReferenceEquals(obj, this) -> true
        | :? ReaderConditional as rc -> 
            let formMatches = if isNull _form then isNull rc.Form else _form.Equals(rc.Form) 
            formMatches && _isSplicing.Equals(rc.IsSplicing)
        | _ -> false

    override this.GetHashCode (): int = 
        Hashing.hashCombine(Hashing.hash(_form), Hashing.hash(_isSplicing))

    interface ILookup with
        member this.valAt (key: obj): obj = (this :> ILookup).valAt(key, null)
        member _.valAt (key: obj, notFound: obj): obj = 
            if FormKeyword.Equals(key) then
                _form
            elif SplicingKeyword.Equals(key) then
                _isSplicing
            else
                notFound
