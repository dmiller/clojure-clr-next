﻿namespace Clojure.Reflection

open Clojure.Collections

open System
open System.Collections.Generic
open System.Reflection


/// Thrown when a type cannot be found.
exception TypeNotFoundException of string

/// Helper class for working with types.
[<AbstractClass; Sealed>]
type RTType private () =


    // TODO: do we still need this?
    /// Returns true if the runtime is Mono.
    static member val IsRunningOnMono = not <| isNull (Type.GetType("Mono.Runtime"))

    // TODO: We need to rethink this completely.

    /// Returns the type named by the string.
    /// Returns null if no type can be found.
    static member ClassForName(p: string) : Type =

        // fastest path, will succeed for assembly qualified names (returned by Type.AssemblyQualifiedName)
        // or namespace qualified names (returned by Type.FullName) in the executing assembly or mscorlib
        // e.g. "UnityEngine.Transform, UnityEngine, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"

        let t = Type.GetType(p, false);

        // Added the IsPublic check to deal with shadowed types in .Net Core,
        // e.g. System.Environment in assemblies System.Private.CoreLib and System.Runtime.Exceptions.
        // It is private in the former and public in the latter.
        // Unfortunately, Type.GetType was finding the former.

        if not <| isNull t && t.IsPublic then
                    t
        else
            // TODO: Will we need this in the new compiler?  I hope we can get rid of it.
            // This code really belongs over in the compiler, but we need RTType for LispReader, so it's here.
            let t = RTType.FindDuplicateType(p)
            if not <| isNull t then
                t
            else
                let t = RTType.FindTypeInAsssemblies(p)
                if not <| isNull t then
                    t
                elif RTType.HasTriggerCharacter(p) then
                    ClrTypeSpec.GetTypeFromName(p)
                else
                    null

    /// Returns the type named by the string.
    /// Throws a TypeNotFoundException if not found.
    static member ClassForNameE(p: string) : Type =
        let t = RTType.ClassForName(p)
        if isNull t then
            raise <| TypeNotFoundException($"Unable to resolve class {p}")
        else
            t

    /// Returns true if the string consists of just a single digit in the range '0'..'9'.
    static member IsPosDigit(s: string) =
        if s.Length <> 1 then false
        else
            let c = s[0]
            c >= '1' && c <= '9'

    /// Returns the primitive type named by the symbol (or null if not the name of a primitive type).
    static member PrimType(sym: Symbol) = 
        if isNull sym then null
        else
            match sym.Name with
            | "int" | "Int32" | "System.Int32" -> typeof<int>
            | "long" | "Int64" | "System.Int64" -> typeof<int64>
            | "float" | "Single" | "System.Single" -> typeof<float>
            | "double" | "Double" | "System.Double" -> typeof<float>
            | "short" | "Int16" | "System.Int16" -> typeof<int16>
            | "byte" | "Byte" | "System.Byte" -> typeof<byte>
            | "sbyte" | "SByte" | "System.SByte" -> typeof<sbyte>
            | "char" | "Char" | "System.Char" -> typeof<char>
            | "bool" | "boolean" | "Boolean" | "System.Boolean" -> typeof<bool>
            | "uint" | "UInt32" | "System.UInt32" -> typeof<uint32>
            | "ulong" | "UInt64" | "System.UInt64" -> typeof<uint64>
            | "ushort" | "UInt16" | "System.UInt16" -> typeof<uint16>
            | "decimal" | "Decimal" | "System.Decimal" -> typeof<decimal>
            | "void" | "Void" | "System.Void" -> typeof<System.Void>
            | _ -> null

    /// A map from primvite types to their Clojure special names.
    static member val private PrimTypeNamesMap = 
         System.Linq.Enumerable.ToDictionary(
             [ (typeof<int>, "int"); 
              (typeof<int64>, "long"); 
              (typeof<float>, "float"); 
              (typeof<int16>, "short"); 
              (typeof<byte>, "byte"); 
              (typeof<sbyte>, "sbyte"); 
              (typeof<char>, "char"); 
              (typeof<bool>, "bool"); 
              (typeof<uint32>, "uint"); 
              (typeof<uint64>, "ulong"); 
              (typeof<uint16>, "ushort"); 
              (typeof<decimal>, "decimal")],
              fst,
              snd)

    /// Returns the Clojure special name for the primitive type (string option).
    static member TryPrimTypeToName(t: Type) = 
        let ok, name = RTType.PrimTypeNamesMap.TryGetValue(t)
        if ok then Some name else None


    /// Returns the corresponding type if the Symbol is the name of one of the special array types (or null if no match).
    static member MaybeSpecialTag(sym: Symbol) = 
        match RTType.PrimType(sym) with
        | null -> 
            match sym.Name with
            | "objects" -> typeof<Object[]>
            | "ints" -> typeof<int[]>
            | "longs" -> typeof<int64[]>
            | "floats" -> typeof<single[]>
            | "doubles" -> typeof<float[]>
            | "shorts" -> typeof<int16[]>
            | "bytes" -> typeof<byte[]>
            | "chars" -> typeof<char[]>
            | "bools" | "booleans" -> typeof<bool[]>
            | "uints" -> typeof<uint32[]>
            | "ulongs" -> typeof<uint64[]>
            | "ushorts" -> typeof<uint16[]>
            | "sbytes" -> typeof<sbyte[]>
            | _ -> null
        | _ as t -> t


    // TODO: Will we need this in the new compiler?  I hope we can get rid of it.
    // This code really belongs over in the compiler, but we need RTType for LispReader, so it's here.
    static member private FindDuplicateType(p: string) : Type = null

    /// Returns the type named by the string, looking through all loaded assemblies.
    static member private FindTypeInAsssemblies(p: string) : Type = 
        let domain = AppDomain.CurrentDomain
        let assemblies = domain.GetAssemblies()


        // fast path, will succeed for namespace qualified names (returned by Type.FullName)
        // e.g. "UnityEngine.Transform"
        // In the original code, this stopped at the first match, not worrying about duplicates.

        let rec fastMatch (i: int) =
            if i < assemblies.Length then
                let a = assemblies.[i]
                let t = a.GetType(p, false)
                if not <| isNull t then
                    t
                else
                    fastMatch (i + 1)
            else
                null

        let slowMatch () =
            let candidateTypes = new List<Type>()
            assemblies
            |> Array.iter (fun a ->
                let t = a.GetType(p, false)
                if not <| isNull t && not <| candidateTypes.Contains(t) then
                    candidateTypes.Add(t))

            match candidateTypes.Count with
            | 0 -> null
            | 1 -> candidateTypes.[0]
            | _ -> raise <| TypeNotFoundException($"Ambiguous type name {p}")
        
        match fastMatch 0 with
        | null -> slowMatch()
        | _ as t -> t

    static member val private _triggerTypeChars = [| '`'; ',' |]
    static member private HasTriggerCharacter(p: string) = p.IndexOfAny(RTType._triggerTypeChars) >= 0

/// Sepcial return type for the ClrTypeSpec.Parse method for array types.
and [<Sealed>] ClrArraySpec(_dimensions: int, _isBound: bool) =

    member internal _.Dimensions = _dimensions
    member internal _.IsBound = _isBound

    member internal this.Resolve(t: Type) = 
        if _isBound then
            t.MakeArrayType(1)
        elif _dimensions = 1 then
            t.MakeArrayType()
        else
            t.MakeArrayType(_dimensions)

// Inspired by similar code in various places
// See  https://github.com/mono/mono/blob/master/mcs/class/corlib/System/TypeSpec.cs
// and see http://www.java2s.com/Open-Source/ASP.NET/Library/sixpack-library/SixPack/Reflection/TypeName.cs.htm 
// The EBNF for fully-qualified type names is here: http://msdn.microsoft.com/en-us/library/yfsftwz6(v=VS.100).aspx
// I primarily followed the mono version. Modifications have to do with assembly and type resolver defaults and some minor details.
// Also, rather than throwing exceptions for badly formed names, we just return null.  Where this is called, generally, an error is not required.
//
// Giving credit where credit is due: please note the following attributions in the mono code:
//
//          Author:
//            Rodrigo Kumpera <kumpera@gmail.com>
//
//          Copyright (C) 2010 Novell, Inc (http://www.novell.com)


and [<Sealed>] ClrTypeSpec() = 

    let mutable _name : string = null
    let mutable _assemblyName : string = null
    let mutable _nested : List<string> = null
    let mutable _genericParams : List<ClrTypeSpec> = null
    let mutable _arraySpec : List<ClrArraySpec> = null
    let mutable _pointerLevel = 0
    let mutable _isByRef = false

    member _.Name = _name
    member _.AssemblyName = _assemblyName
    member _.Nested = _nested
    member _.GenericParams = _genericParams
    member _.ArraySpec = _arraySpec
    member _.PointerLevel = _pointerLevel
    member _.IsByRef = _isByRef

    member _.HasGenericParams = not <| isNull _genericParams   
    member _.IsArray = not <| isNull _arraySpec
    member _.IsNested = not <| isNull _nested

    member private _.AddName( typeName : string ) =
        if isNull _name then    
            _name <- typeName
        else
            if isNull _nested then  _nested <- new List<string>()
            _nested.Add(typeName)

    member private _.SetAssemblyName( assyName : string ) =
        _assemblyName <- assyName
        
    member private _.AddArray(arraySpec : ClrArraySpec) =
        if isNull _arraySpec then _arraySpec <- new List<ClrArraySpec>()
        _arraySpec.Add(arraySpec)

    member private _.IncrementPointerLevel() =
        _pointerLevel <- _pointerLevel + 1

    member private _.SetIsByRef() =
        _isByRef <- true

    member private _.SetGenericArgs( args : List<ClrTypeSpec> ) =
        _genericParams <- args

    // entry point
    static member GetTypeFromName(name: string) : Type =

        let defaultAssemblyResolver(assyName: AssemblyName) : Assembly = 
            Assembly.Load(assyName)

        let defaultTypeResolver (assy: Assembly) ( typeName: string) :Type = 
                    if isNull assy then
                        if name.Equals(typeName) then
                            null
                        else
                            RTType.ClassForName(typeName)
                    else assy.GetType(typeName)

        match ClrTypeSpec.Parse(name) with
        | Error _ -> null
        | Ok spec -> spec.Resolve(defaultAssemblyResolver, defaultTypeResolver)



    static member internal Parse(name: string) : Result<ClrTypeSpec,string> =
        let mutable pos = 0
        let spec = ClrTypeSpec.Parse(name, &pos, false, false)
        match spec with
        | Error _ -> spec
        | _ when pos < name.Length -> Error("Could not parse the whole type name")
        | _ -> spec

    static member internal Parse(name: string, pos: byref<int>, isRecursive: bool, allowAssyQualName: bool) : Result<ClrTypeSpec,string> =
        
        let mutable nameStart = 0
        let mutable hasModifiers = false
        let mutable status : Result<ClrTypeSpec,string> option = None
        let spec = ClrTypeSpec()

        pos <-  ClrTypeSpec.SkipSpace(name, pos)  
        nameStart <- pos

        while ( pos < name.Length && status.IsNone && not hasModifiers) do
            match name[pos] with
            | '+' ->            
                spec.AddName(name.Substring(nameStart, pos - nameStart))
                nameStart <- pos + 1
            | ',' 
            | ']' ->
                spec.AddName(name.Substring(nameStart, pos - nameStart))
                nameStart <- pos + 1
                if isRecursive && not allowAssyQualName then
                    status <- Some (Ok spec)
                else
                    hasModifiers <- true
            | '&'
            | '*' 
            | '[' ->
                if name[pos] <> '['  && isRecursive then
                    status <- Some (Error "Generic argument can't be byref or pointer type")
                else
                    spec.AddName(name.Substring(nameStart, pos - nameStart))
                    nameStart <- pos + 1
                    hasModifiers <- true
            | _ -> ()

            if status.IsNone && not hasModifiers then 
                pos <- pos + 1
        
        if status.IsSome then
            status.Value
        else
            
            if nameStart < pos then
                spec.AddName(name.Substring(nameStart, pos - nameStart))

            if hasModifiers then

                while pos < name.Length && status.IsNone do
                    match name[pos] with
                    | '&' ->
                        if spec.IsByRef then
                            status <- Some (Error "Can't have more than one byref modifier")
                        else 
                            spec.SetIsByRef()
                    | '*' ->
                        if spec.IsByRef then
                            status <- Some (Error "Can't have pointer modifier on byref type")
                        else 
                            spec.IncrementPointerLevel()
                    | ',' -> 
                        if isRecursive then
                            let mutable endPos = pos
                            while endPos < name.Length && name[endPos] <> ']' do
                                endPos <- endPos + 1
                            if endPos >= name.Length then
                                status <- Some (Error "Unmatched ']' while parsing generic argument assembly name")
                            else 
                                spec.SetAssemblyName(name.Substring(pos + 1, endPos - pos - 1).Trim())
                                pos <- endPos + 1
                                status <- Some (Ok spec)
                        else
                            spec.SetAssemblyName(name.Substring(pos + 1).Trim())
                            pos <- name.Length
                    | '[' -> 
                        if spec.IsByRef then
                            status <- Some (Error "Byref qualifier must be the last one of a type")
                        else 
                            pos <- pos + 1
                            if pos >= name.Length then
                                status <- Some (Error "Invalid array/generic spec")
                            else 
                                pos <- ClrTypeSpec.SkipSpace(name, pos)
                                if name[pos] <> ',' && name[pos] <> '*' && name[pos] <> ']' then
                                    // generic args
                                    let args = new List<ClrTypeSpec>()
                                    if spec.IsArray then 
                                        status <- Some (Error "generic args after array spec")
                                    else 
                                        let mutable finished = false
                                        while pos < name.Length && status.IsNone && not finished do
                                            pos <- ClrTypeSpec.SkipSpace(name, pos)
                                            let aqn = name[pos] = '['
                                            if aqn then
                                                pos <- pos + 1  // skip [ to the start of the type
                                            let arg = ClrTypeSpec.Parse(name, &pos, true, aqn)
                                            match arg with
                                            | Error _ -> status <- Some arg   
                                            | Ok argSpec -> 
                                                args.Add(argSpec)                                         
                                                if pos >= name.Length then
                                                    status <- Some (Error "Invalid generic arguments spec")
                                                elif name[pos] = ']' then
                                                    finished <- true  // end of generic args
                                                elif name[pos] = ',' then
                                                    pos <- pos + 1  // skip ',' to start of the next arg
                                                else
                                                    status <- Some (Error "Invalid generic arguments separator")
                                        if status.IsNone && ( pos >= name.Length || name[pos] <> ']') then
                                            status <- Some (Error "Error parsing generic params spec")
                                        else
                                            spec.SetGenericArgs(args)
                                else  
                                    // array spec
                                    let mutable dimensions = 1
                                    let mutable isBound = false
                                    while pos < name.Length && name[pos] <> ']' && status.IsNone do
                                        if name[pos] = '*' then
                                            if isBound then
                                                status <- Some (Error "Array spec cannot have 2 bound dimensions")
                                            else
                                                isBound <- true
                                        elif name[pos] <> ',' then
                                            status <- Some (Error $"Invalid character in array spec: {name[pos]}")
                                        else
                                            dimensions <- dimensions + 1

                                        if status.IsNone then
                                            pos <- pos + 1
                                            pos <- ClrTypeSpec.SkipSpace(name, pos)
                                    if status.IsNone then
                                        if name[pos] <> ']' then
                                            status <- Some (Error "Unmatched '[' while parsing array spec")
                                        elif dimensions > 1 && isBound then
                                            status <- Some (Error "Invalid array spec, multi-dimensional array cannot be bound")
                                        else
                                            spec.AddArray(ClrArraySpec(dimensions, isBound))
                    | ']' ->
                        if isRecursive then 
                            pos <- pos + 1
                            status <- Some (Ok spec)
                        else 
                            status <- Some (Error "Unmatched ']' while parsing type name")
                    | _ -> status <- Some (Error $"Bad type def, can't handle {name[pos]} at {pos}")

                    if status.IsNone then
                        pos <- pos + 1

            match status with
            | Some result -> result
            | None -> Ok spec

    static member private SkipSpace(name: string, pos: int) : int =
        
        let rec loop (i : int) =
            if i < name.Length && Char.IsWhiteSpace(name[i]) then loop (i+1)
            else i

        loop pos
        

    member internal _.Resolve(assemblyResolver : Func<AssemblyName, Assembly>, typeResolver : Func<Assembly, string, Type>) : Type =
        let assyRequired = not <| isNull _assemblyName

        let assy = 
            if assyRequired then
                    match assemblyResolver with
                    | null -> Assembly.Load(_assemblyName)
                    | _ -> assemblyResolver.Invoke(AssemblyName(_assemblyName))   
            else
                null

        if assyRequired && isNull assy then
            null
        else
            try // we'll consider not finding a component an error and raise.  Maybe some computation expression magic would be nicer.
                let mutable t = typeResolver.Invoke(assy, _name)
                if isNull t then
                    null
                else
                    if not <| isNull _nested then
                        for n in _nested do
                            let temp = t.GetNestedType(n, BindingFlags.Public ||| BindingFlags.NonPublic)
                            if isNull temp then raise <| ArgumentException($"Unable to resolve nested type {n}")
                            t <- temp

                    if not <| isNull _genericParams then
                        let args = Array.zeroCreate<Type> _genericParams.Count
                        for i = 0 to args.Length - 1  do
                            let temp = _genericParams[i].Resolve(assemblyResolver, typeResolver)
                            if isNull temp then raise <| ArgumentException($"Unable to resolve generic parameter {i}")
                            args[i] <- temp
                        t <- t.MakeGenericType(args)

                    for a in _arraySpec do
                        t <- a.Resolve(t)

                    for i in 0 .. _pointerLevel - 1 do
                        t <- t.MakePointerType()

                    if _isByRef then
                        t <- t.MakeByRefType()
                    t
            with
            | _ -> null


        
