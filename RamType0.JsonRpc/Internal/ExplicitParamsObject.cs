﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
namespace RamType0.JsonRpc.Internal
{
    using Server;
    using Utf8Json;

    public delegate TResult ExplicitParamsFunc<TParams, TResult>(TParams parameters);
    public delegate void ExplicitParamsAction<TParams>(TParams parameters);
    public struct ExplicitParamsObjectDeserializer<T> : IParamsDeserializer<T>
    {
        public T Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
        {
            return formatterResolver.GetFormatterWithVerify<T>().Deserialize(ref reader, formatterResolver);
        }
    }

    internal static class ExplicitParamsModifierCache<TParams>
    {
        internal static Type ModifierType { get; }
        static ExplicitParamsModifierCache()
        {
            FieldInfo? idInjectField = null;
            foreach (var field in typeof(TParams).GetFields(BindingFlags.Public | BindingFlags.NonPublic))
            {
                if(field.GetCustomAttribute<RpcIDAttribute>() is null)
                {
                    continue;
                }
                else
                {
                    idInjectField = field;
                    break;
                }
            }
            if(idInjectField is null)
            {
                ModifierType = typeof(EmptyModifier<TParams>);
            }
            else
            {
                ModifierType = RpcEntryFactoryHelper.CreateIdInjecter(typeof(TParams).FullName + ".IdInjecter", idInjectField, typeof(TParams));
            }

        }
    }

    internal static class RpcExplicitParamsFuncDelegateEntryFactory<TParams, TResult>
    {
        public static RpcEntryFactory<ExplicitParamsFunc<TParams,TResult>> Instance { get; }
        static RpcExplicitParamsFuncDelegateEntryFactory()
        {
            Instance = RpcDelegateEntryFactory<ExplicitParamsFunc<TParams, TResult>>.CreateDelegateEntryFactory(typeof(TParams), typeof(TResult), typeof(ExplicitParamsObjectDeserializer<TParams>), ExplicitParamsModifierCache<TParams>.ModifierType,typeof(ExplicitParamsFuncDelegateInvoker<TParams, TResult>));
        }
    }

    internal static class RpcExplicitParamsActionDelegateEntryFactory<TParams>
    {
        public static RpcEntryFactory<ExplicitParamsAction<TParams>> Instance { get; }
        static RpcExplicitParamsActionDelegateEntryFactory()
        {
            Instance = RpcDelegateEntryFactory<ExplicitParamsAction<TParams>>.CreateDelegateEntryFactory(typeof(TParams), typeof(NullResult), typeof(ExplicitParamsObjectDeserializer<TParams>), ExplicitParamsModifierCache<TParams>.ModifierType, typeof(ExplicitParamsActionDelegateInvoker<TParams>));
        }
    }

    public struct ExplicitParamsFuncDelegateInvoker<TParams, TResult> : IRpcMethodBody<TParams, TResult>,IDelegateContainer<ExplicitParamsFunc<TParams, TResult>>
    {
        public ExplicitParamsFunc<TParams, TResult> Delegate { get; set; }

        public TResult Invoke(TParams parameters)
        {
            return Delegate(parameters);
        }
    }

    public struct ExplicitParamsActionDelegateInvoker<TParams> : IRpcMethodBody<TParams, NullResult>, IDelegateContainer<ExplicitParamsAction<TParams>>
    {
        public ExplicitParamsAction<TParams> Delegate { get; set; }

        public NullResult Invoke(TParams parameters)
        {
            Delegate(parameters);
            return new NullResult();
        }
    }
}