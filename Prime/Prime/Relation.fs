﻿// Prime - A PRIMitivEs code library.
// Copyright (C) Bryan Edds, 2013-2017.

namespace Prime
open System
open System.ComponentModel
open System.Reflection
open Prime
    
/// Converts Relation types.
type RelationConverter (targetType : Type) =
    inherit TypeConverter ()
    
    override this.CanConvertTo (_, destType) =
        destType = typeof<string> ||
        destType = typeof<Symbol> ||
        destType = targetType
        
    override this.ConvertTo (_, _, source, destType) =
        if destType = typeof<string> then
            let toStringMethod = targetType.GetMethod "ToString"
            toStringMethod.Invoke (source, null)
        elif destType = typeof<Symbol> then
            let toStringMethod = targetType.GetMethod "ToString"
            let relationStr = toStringMethod.Invoke (source, null) :?> string
            if Symbol.shouldBeExplicit relationStr then String (relationStr, None) :> obj
            else Atom (relationStr, None) :> obj
        elif destType = targetType then source
        else failconv "Invalid RelationConverter conversion to source." None
        
    override this.CanConvertFrom (_, sourceType) =
        sourceType = typeof<string> ||
        sourceType = typeof<Symbol> ||
        sourceType = targetType
        
    override this.ConvertFrom (_, _, source) =
        match source with
        | :? string as fullName ->
            let makeFromStringFunction = targetType.GetMethod ("makeFromString", BindingFlags.Static ||| BindingFlags.Public)
            let makeFromStringFunctionGeneric = makeFromStringFunction.MakeGenericMethod ((targetType.GetGenericArguments ()).[0])
            makeFromStringFunctionGeneric.Invoke (null, [|fullName|])
        | :? Symbol as relationSymbol ->
            match relationSymbol with
            | Atom (fullName, _) | String (fullName, _) ->
                let makeFromStringFunction = targetType.GetMethod ("makeFromString", BindingFlags.Static ||| BindingFlags.Public)
                let makeFromStringFunctionGeneric = makeFromStringFunction.MakeGenericMethod ((targetType.GetGenericArguments ()).[0])
                makeFromStringFunctionGeneric.Invoke (null, [|fullName|])
            | Number (_, _) | Quote (_, _) | Symbols (_, _) ->
                failconv "Expected Symbol or String for conversion to Relation." ^ Some relationSymbol
        | _ ->
            if targetType.IsInstanceOfType source then source
            else failconv "Invalid RelationConverter conversion from source." None

[<AutoOpen>]
module RelationModule =

    /// A relation that can be resolved to an address via projection.
    type [<CustomEquality; NoComparison; TypeConverter (typeof<RelationConverter>)>] 'a Relation =
        private
            { NameOpts : string option list
              TypeCarrier : 'a -> unit }
    
        /// Make a relation from a '/' delimited string where '?' are empty.
        /// NOTE: do not move this function as the RelationConverter's reflection code relies on it being exactly here!
        static member makeFromString<'a> (relationStr : string) =
            let nameOptList = relationStr.Split Constants.Address.Separator |> List.ofSeq
            let nameOpts = List.map (fun name -> match name with Constants.Relation.SlotStr -> None | _ -> Some name) nameOptList
            { NameOpts = nameOpts; TypeCarrier = fun (_ : 'a) -> () }

        /// Hash a Relation.
        static member hash (relation : 'a Relation) =
            List.hash relation.NameOpts
                
        /// Equate Relations.
        static member equals relation relation2 =
            relation.NameOpts = relation2.NameOpts

        /// Resolve a relationship to an address.
        static member resolve<'a, 'b> (address : 'a Address) (relation : 'b Relation) =
            let names = List.project id (Address.getNames address) relation.NameOpts
            Address.makeFromList<'b> names

        interface 'a Relation IEquatable with
            member this.Equals that =
                Relation<'a>.equals this that
    
        override this.Equals that =
            match that with
            | :? ('a Relation) as that -> Relation<'a>.equals this that
            | _ -> false
    
        override this.GetHashCode () =
            Relation<'a>.hash this
        
        override this.ToString () =
            let names = List.map (fun nameOpt -> match nameOpt with Some name -> name | None -> Constants.Relation.SlotStr) this.NameOpts
            String.concat Constants.Address.SeparatorStr names

    [<RequireQualifiedAccess>]
    module Relation =

        /// Make a relation from a list of option names.
        let makeFromList<'a> nameOptsList =
            { NameOpts = nameOptsList |> List.ofSeq; TypeCarrier = fun (_ : 'a) -> () }
    
        /// Make a relation from a '/' delimited string.
        let makeFromString<'a> relationStr =
            Relation<'a>.makeFromString relationStr

        /// Get the optional names of a relation.
        let getNameOpts relation =
            relation.NameOpts

        /// Change the type of an address.
        let changeType<'a, 'b> (relation : 'a Relation) =
            { NameOpts = relation.NameOpts; TypeCarrier = fun (_ : 'b) -> () }

/// A relation that can be resolved to an address via projection.
type 'a Relation = 'a RelationModule.Relation