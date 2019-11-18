﻿using System;
using System.Collections.Generic;
using System.Text;
using Utf8Json;
using System.Reflection.Emit;
using System.Reflection;
using System.Threading.Tasks;
using RamType0.JsonRpc.Emit;
using System.Runtime.CompilerServices;

namespace RamType0.JsonRpc
{
    public class JsonRpcMethodDictionary
    {
        Dictionary<EscapedUTF8String, RpcInvoker> RpcMethods { get; } = new Dictionary<EscapedUTF8String, RpcInvoker>();

        //delegate Task RpcInvokerMethod(IResponser responser, ref JsonReader reader, ID? id, IJsonFormatterResolver formatterResolver);
       
        public Task InvokeAsync(IResponser responser,EscapedUTF8String methodName,ID? id,ref JsonReader reader,IJsonFormatterResolver formatterResolver)
        {
            if(RpcMethods.TryGetValue(methodName,out var invoker))
            {
                return invoker.ReadParamsAndInvokeAsync(responser, ref reader, id, formatterResolver);
            }
            else
            {
                if (id is ID reqID)
                {
                    return Task.Run(() => responser.Response(ErrorResponse.MethodNotFound(reqID, methodName.ToString())));
                }
                else
                {
                    return Task.CompletedTask;
                }
            }
        }

        public void Register<T>(string methodName,T method)
            where T:Delegate
        {
            var paramsType = MethodParamsTypeBuilder.CreateParamsType(method, methodName);
            var invoker = Unsafe.As<RpcInvoker>(Activator.CreateInstance(typeof(RpcInvoker<>).MakeGenericType(paramsType)));
            //var invoker = Unsafe.As<RpcInvokerMethod>(typeof(RpcInvoker).GetMethod("ReadParamsAndInvokeAsync")!.MakeGenericMethod(paramsType).CreateDelegate(typeof(RpcInvokerMethod)));
            RpcMethods.Add(methodName, invoker);
        }
   
        abstract class RpcInvoker:IDisposable
        {
            public abstract void ReleasePooledClosures();
            
            public abstract Task ReadParamsAndInvokeAsync(IResponser responser, ref JsonReader reader, ID? id, IJsonFormatterResolver formatterResolver);
            public abstract void Dispose();
        }

        sealed class RpcInvoker<TParams> : RpcInvoker
             where TParams : struct, IMethodParamsObject
        {
            public override void ReleasePooledClosures()
            {
                RpcMethodClosure<TParams>.ReleasePooledClosures();
            }

            public override void Dispose()
            {
                DisposeClosuresAndDelegate();
                GC.SuppressFinalize(this);
            }

            private void DisposeClosuresAndDelegate()
            {

                RpcMethodClosure<TParams>.Dispose();
                default(TParams).Dispose();
            }

            ~RpcInvoker()
            {
                DisposeClosuresAndDelegate();
            }

            /// <summary>
            /// このメソッドの呼び出し後、readerの状態は未定義です。使用する場合、予めコピーしておいてください。
            /// </summary>
            /// <param name="reader">RequestObject全体を与えられた<see cref="JsonReader"/>。</param>
            /// <param name="id"></param>
            /// <param name="formatterResolver"></param>
            /// <returns></returns>
            public override Task ReadParamsAndInvokeAsync(IResponser responser, ref JsonReader reader, ID? id, IJsonFormatterResolver formatterResolver)

            {
                reader.ReadIsBeginObjectWithVerify();
                ReadOnlySpan<byte> paramsStr = stackalloc byte[] { (byte)'p', (byte)'a', (byte)'r', (byte)'a', (byte)'m', (byte)'s', };
                TParams parameters;
                while (true)
                {

                    JsonToken token = reader.GetCurrentJsonToken();
                    switch (token)
                    {
                        case JsonToken.String:
                            //IsProperty
                            {
                                //IsParams
                                if (reader.ReadPropertyNameSegmentRaw().AsSpan().SequenceEqual(paramsStr))
                                {

                                    var copyReader = reader;
                                    try
                                    {
                                        parameters = ParamsFormatter<TParams>.Instance.Deserialize(ref reader, formatterResolver);
                                    }
                                    catch (JsonParsingException)
                                    {
                                        if (id is ID reqID)
                                        {
                                            var paramsJson = Encoding.UTF8.GetString(copyReader.ReadNextBlockSegment());//このメソッドが呼ばれた時点でParseErrorはありえない
                                            return Task.Run(() => responser.Response(ErrorResponse.InvalidParams(reqID, paramsJson)));
                                        }
                                        else
                                        {
                                            return Task.CompletedTask;//TODO:ValueTaskのほうがええんか・・・？
                                        }

                                    }
                                    goto Invoke;
                                }
                                else
                                {
                                    reader.ReadNextBlock();
                                    reader.ReadIsValueSeparatorWithVerify();
                                    continue;
                                }
                            }
                        case JsonToken.EndObject:
                            {
                                if (default(TParams) is IEmptyParamsObject)
                                {
                                    parameters = default;
                                    goto Invoke;
                                }
                                else
                                {
                                    if (id is ID reqID)
                                    {
                                        return Task.Run(() => responser.Response(ErrorResponse.InvalidParams(reqID, "(not exists)")));
                                    }
                                    else
                                    {
                                        return Task.CompletedTask;
                                    }
                                }
                            }

                        default:
                            {
                                throw new JsonParsingException($"Expected property or end of object,but {((char)reader.GetBufferUnsafe()[reader.GetCurrentOffsetUnsafe()]).ToString()}");
                            }
                    }
                 
                }
                Invoke:
                //paramsが読み取れた
                var closure = RpcMethodClosure<TParams>.GetClosure(responser, parameters, id);
                return Task.Run(closure.InvokeAction);

            }

            

        }




    }


}
