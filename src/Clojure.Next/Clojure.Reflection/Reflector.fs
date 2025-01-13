namespace Clojure.Reflection

open System
open System.Collections.Generic
open System.Reflection

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
            | null -> raise <| new InvalidOperationException($"Cannot find constructor for type: {RTType.NameForType(t)} with the correct argument types")
            | _ -> found.Invoke(Reflector.BoxArgs(found.GetParameters(), args))
            

    static member InvokeStaticMethod(typeName: string, methodName: string, args: obj array) =
        let t = RTType.ClassForNameE(typeName)
        Reflector.InvokeStaticMethod(t, methodName, args)

    static member InvokeStaticMethod(t: Type, methodName: string, args: obj array) =
        let method = t.GetMethod(methodName, BindingFlags.Public ||| BindingFlags.Static)
        if isNull method then
            raise <| new ArgumentException($"No method {methodName} in type {t.FullName}")
        else
            method.Invoke(null, Reflector.BoxArgs(method.GetParameters(), args))


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
        


