namespace Clojure.IO

open Clojure.Collections
open Clojure.Numerics


[<Sealed>]
type TaggedLiteral private (_tag: Symbol, _form: obj) = 
    
    static member val public TagKeyword = Keyword.intern("tag")
    static member val public FormKeyword = Keyword.intern("form")

    static member create(tag: Symbol, form: obj) = TaggedLiteral(tag, form)

    member this.Tag = _tag
    member this.Form = _form

    interface ILookup with  
        member this.valAt (key: obj): obj = (this :> ILookup).valAt(key, null)
        member this.valAt (key: obj, notFound: obj): obj =
            if TaggedLiteral.FormKeyword.Equals(key) then
                _form
            elif TaggedLiteral.TagKeyword.Equals(key) then
                _tag
            else
                notFound

    override this.Equals (o: obj): bool = 
        match o with 
        | _ when LanguagePrimitives.PhysicalEquality o this -> true
        | :? TaggedLiteral as tl ->
             ( if isNull _form then isNull tl.Form else _form.Equals(tl.Form)) &&
             (if isNull _tag then isNull tl.Tag else _tag.Equals(tl.Tag)) 
        | _ -> false
        
    override _.GetHashCode() =  Hashing.hashCombine(Hashing.hash(_tag), Hashing.hash(_form))

        
