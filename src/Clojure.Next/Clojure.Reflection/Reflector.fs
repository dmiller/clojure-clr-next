namespace Clojure.Reflection

open Clojure.Collections

open System
open System.Collections.Generic
open System.Reflection
open System.Text
open System.Linq

[<AbstractClass; Sealed>]
type Reflector private () =

    static member InvokeConstructor(t: Type, args : obj array) = 
        // TODO:  (original)  Replace with GetConstructors/GetMatchingMethodAux

        let ctors = 
            t.GetConstructors()
            |> Array.where (fun c -> c.GetParameters().Length = args.Length)

        if ctors.Length = 0 then
            if t.IsValueType && args.Length = 0 then
                // invoke default constructor for value types
                Activator.CreateInstance(t)
            else
                raise <| new ArgumentException($"No matching constructor found for {t.FullName}")
        elif ctors.Length = 1 then
            let ctor = ctors[0]
            ctor.Invoke(Reflector.BoxArgs(ctor.GetParameters(), args))
        else
            // More than one constructor with the correct arity.  Find best match

            let mutable found: ConstructorInfo = null
            for i = 0 to ctors.Length - 1 do
                let c = ctors[i]
                let pis = c.GetParameters()
                if Reflector.IsCongruent(pis, args) && ( found = null  || Reflector.Subsumes(pis, found.GetParameters()) ) then
                        found <- c

            match found with
            | null -> raise <| new InvalidOperationException($"Cannot find constructor for type: {Util.nameForType(t)} with the correct argument types")
            | _ -> found.Invoke(Reflector.BoxArgs(found.GetParameters(), args))
            

    static member InvokeStaticMethod(typeName: string, methodName: string, args: obj array) =
        let t = RTType.ClassForNameE(typeName)
        Reflector.InvokeStaticMethod(t, methodName, args)

    static member InvokeStaticMethod(t: Type, methodName: string, args: obj array) =
        if methodName.Equals("new") then
            Reflector.InvokeConstructor(t, args)
        else
            let methods = Reflector.GetMethods(t, methodName, GenericTypeArgList.Empty, args.Length, true)
            Reflector.InvokeMatchingMethod(methodName, methods, t, null, args)

    static member GetMethods(targetType: Type, methodName: string, typeArgs: GenericTypeArgList, arity: int, getStatics: bool)  : IList<MethodBase> =
        let flags = BindingFlags.Public ||| BindingFlags.FlattenHierarchy ||| BindingFlags.InvokeMethod  ||| (if getStatics then BindingFlags.Static else BindingFlags.Instance)

        if targetType.IsInterface && not getStatics then
            Reflector.GetInterfaceMethods(targetType, methodName, typeArgs, arity).Cast<MethodBase>().ToList()
            
        else
            (targetType.GetMethods(flags).Where(fun m -> m.Name = methodName && m.GetParameters().Length = arity)
       
            |> Seq.choose (fun m -> 
                                let genArgs = m.GetGenericArguments()
                                if typeArgs.IsEmpty && genArgs.Length = 0 then
                                    Some m
                                elif not typeArgs.IsEmpty && typeArgs.Count = genArgs.Length then
                                    Some  <| m.MakeGenericMethod(typeArgs.ToArray())
                                else
                                    None)
                                    
             |> Seq.cast<MethodBase>).ToList()

    // We are returning list of MethodBase rather than MethodInfo?  Above and below.

    static member GetInterfaceMethods(targetType: Type, methodName: string, typeArgs: GenericTypeArgList, arity: int) : List<MethodBase> =
        let flags = BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.InvokeMethod
        let interfaces = List<Type>([| targetType |])
        interfaces.AddRange(targetType.GetInterfaces())
        (interfaces
        |> Seq.collect (fun i -> i.GetMethods(flags).Where(fun m -> m.Name = methodName && m.GetParameters().Length = arity))
        |> Seq.choose (fun m -> 
                let genArgs = m.GetGenericArguments()
                if typeArgs.IsEmpty && genArgs.Length = 0 then
                    Some m
                elif not typeArgs.IsEmpty && typeArgs.Count = genArgs.Length then
                    Some  <| m.MakeGenericMethod(typeArgs.ToArray())
                else
                    None)
         |> Seq.cast<MethodBase>).ToList()


    static member InvokeMatchingMethod(methodName: string, infos: IList<MethodBase>, t: Type, target: obj, args: obj array) =
        let targetType = if isNull t then target.GetType() else t

        let info : MethodBase =
            if infos.Count = 0 then
                null
            elif infos.Count = 1 then
                infos[0] 
            else
                let mutable found: MethodBase = null
                for i = 0 to infos.Count - 1 do
                    let m = infos.[i]
                    let pis = m.GetParameters()
                    if Reflector.IsCongruent(pis, args) && ( found = null  || Reflector.Subsumes(pis, found.GetParameters()) ) then
                        found <- m
                found

        if isNull info then
            raise <| new ArgumentException( $"""Cannot find {if isNull t then "instance" else "static"} method named: {methodName} for type: {targetType.Name} with {args.Length} arguments""")
    
        Reflector.InvokeMethod(info :?> MethodInfo, target, args)

    static member InvokeMethod(info: MethodInfo, target: obj, args: obj array) =
        let boxedArgs = Reflector.BoxArgs(info.GetParameters(), args)

        if info.ReturnType = typeof<Void> then
            info.Invoke(target, boxedArgs) |> ignore
            null
        else
            Reflector.prepRet(info.ReturnType, info.Invoke(target, boxedArgs))

    static member prepRet(t:Type, x: obj) = x
    // at some point, this used to be more complicated
            
            //if (!t.IsPrimitive)
            //    return x;

            //if (x is Boolean)
            //    //return ((Boolean)x) ? RT.T : RT.F;
            //    return ((Boolean)x) ? true : false;
            //else if (x is Int32)
            //    return (long)(int)x;
            ////else if (x is Single)
            ////    return (double)(float)x;


    // TODO: How can this even be correct
    static member Subsumes(c1 : ParameterInfo array, c2 : ParameterInfo array) =
        // presumes c1 and c2 have the same length

        let rec loop (i:int) (better:bool) =
            if i >= c1.Length then
                better
            else
                let t1 = c1.[i].ParameterType
                let t2 = c2.[i].ParameterType
                if t1 <> t2 then
                    if not t1.IsPrimitive && t2.IsPrimitive || t2.IsAssignableFrom(t1) then
                        loop (i + 1) true
                    else
                        false
                else
                    loop (i + 1) better

        loop 0 false

            

    static member BoxArgs(pinfos: ParameterInfo array, args: obj array) =
        if pinfos.Length = 0 then 
            null
        else
            args
            |> Array.zip pinfos
            |> Array.map Reflector.BoxArg


    static member BoxArg(pi: ParameterInfo, arg: obj) =
        if isNull arg then arg
        else
            let paramType = pi.ParameterType
            if not paramType.IsValueType then
                Convert.ChangeType(arg, paramType)
            else
                arg

    static member IsCongruent(pinfos: ParameterInfo array, args: obj array) =
        if isNull args then
            pinfos.Length = 0
        elif pinfos.Length <> args.Length then
            false
        else
            args
            |> Array.zip pinfos
            |> Array.forall (fun (pi, arg) -> Reflector.ParamArgTypeMatch(pi.ParameterType, if isNull arg then null else arg.GetType()))


    static member ParamArgTypeMatch(paramType: Type, argType: Type) =
        if isNull argType then
            not paramType.IsValueType
        else
            Reflector.AreAssignable(paramType, argType)


    // Stolen from DLR TypeUtils
    static member AreAssignable(dest: Type, src: Type) =
        if dest = src then
            true
        elif dest.IsAssignableFrom(src) then
            true
        elif dest.IsArray && src.IsArray && dest.GetArrayRank() = src.GetArrayRank() && Reflector.AreReferenceAssignable(dest.GetElementType(), src.GetElementType()) then
            true
        elif src.IsArray && dest.IsGenericType &&
          (dest.GetGenericTypeDefinition() = typedefof<IEnumerable<_>> || 
           dest.GetGenericTypeDefinition() = typedefof<IList<_>>       || 
           dest.GetGenericTypeDefinition() = typedefof<ICollection<_>>)   &&
           dest.GetGenericArguments()[0] = src.GetElementType() then
            true
        else
            false

    // Stolen from DLR TypeUtils
    static member AreReferenceAssignable(dest: Type, src:Type) = 
        // WARNING: This actually implements "Is this identity assignable and/or reference assignable?"
        if dest = src then
            true
        elif not dest.IsValueType && not src.IsValueType && Reflector.AreAssignable(dest,src) then
            true
        else
            false
        

and [<Sealed>] GenericTypeArgList private (_typeArgs : List<Type>) = 
    
    // An empty instance
    // In the semantics of (type-args ...), (type-args) is equivalent to not having any type args at all -- it is ignored.
    static member val Empty = GenericTypeArgList(List<Type>())

    // Some places need a list of the types, some places need an array.

    member _.ToArray() = _typeArgs.ToArray()
    member _.ToList() = _typeArgs.AsReadOnly()

    member _.IsEmpty = not <| _typeArgs.Any()
    member _.Count = _typeArgs.Count

    static member Create(targs: ISeq) =
        let types = new List<Type>()

        let rec loop (s: ISeq) =
            match s with
            | null -> ()
            | _ ->
                let arg = s.first()
                if not <| arg :? Symbol then
                    raise <| new ArgumentException("Malformed generic method designator: type arg must be a Symbol")
                else
                    let t = RTType.MaybeType(arg, false)
                    if isNull t then
                        raise <| new ArgumentException($"Malformed generic method designator: invalid type arg")
                    else
                        types.Add(t)
                        loop (s.next())

        loop targs
        GenericTypeArgList(types)

    member _.GenerateGenericTypeArgsString() =
        let sb = StringBuilder()
        sb.Append("<") |> ignore
        _typeArgs.ForEach (fun t -> sb.Append(t.FullName).Append(",") |> ignore)
        sb.Append(">") |> ignore
        sb.ToString()