using System;
using System.Runtime.InteropServices;

namespace DOB
{
    public unsafe class DataOrientedBuffer
    {
        public struct Container<T>
        where T : unmanaged
        {
            private IntPtr bufferPtr;
            private int length;
            private int capacity;

            public Container(IntPtr bufPtr, int len, int maxItems)
            {
                bufferPtr = bufPtr;
                length    = len;
                capacity  = maxItems;
            }

            public void SetBufferPtr(IntPtr ptr)
            {
                bufferPtr = ptr;
            }

            public int GetCapacity()
            {
                return capacity;
            }

            public int GetLength()
            {
                return length;
            }

            public void RemoveIndex(int index)
            {
                if (length > 0)
                {
                    if (index < length)
                    {
                        if (index < length - 1)
                        {
                            int numAffected = length - index - 1;
                            int itemSize = sizeof(T);
                            int srcBytes = itemSize * numAffected;
                            IntPtr srcPtr = bufferPtr + (itemSize * (index + 1));
                            IntPtr dstPtr = bufferPtr + (itemSize * index);
                            Buffer.MemoryCopy((void*)srcPtr, (void*)dstPtr, srcBytes, srcBytes);
                        }
                        length--;
                    }
                }
            }

            public bool TryAppend(T comp)
            {
                if (length == capacity) { return false; }

                this[length++] = comp;
                return true;
            }

            public T this[int index]
            {
                get => ((T*)bufferPtr)[index];
                set => ((T*)bufferPtr)[index] = value;
            }
        }

        public struct Entity
        {
            public uint entityID;
            public bool isEnabled;
            public bool isValid;
        }

        public struct ComponentMetadata
        {
            public long type;
            public long bufferItemType;
            public long groupType;
            public int typeSize;
            public int bufferItemTypeSize;
            public IntPtr compPtr;
            public IntPtr bufferPtr;
            public int maxBufferItems;
            public bool isBuffer;
        }

        class ComponentBuffer
        {
            public ComponentMetadata[] metaData;
            IntPtr compsPtr;
            int entityCount;
            int maxEntityCount;
            uint currentEntityID;
            ushort componentCount;
            bool hasAllocated;

            public void RemoveIndex(int index)
            {
                int entitiesToMove = entityCount - index - 1;
                if (entityCount > 0)
                {
                    if (index < entityCount)
                    {
                        if (index < entityCount - 1)
                        {
                            for (int i = 0; i < componentCount; i++)
                            {
                                int srcBytes = metaData[i].typeSize * entitiesToMove;
                                IntPtr srcPtr = metaData[i].compPtr + (metaData[i].typeSize * (index + 1));
                                IntPtr dstPtr = metaData[i].compPtr + (metaData[i].typeSize * index);
                                Buffer.MemoryCopy((void*)srcPtr, (void*)dstPtr, srcBytes, srcBytes);

                                if (metaData[i].isBuffer)
                                {
                                    srcBytes = metaData[i].bufferItemTypeSize * metaData[i].maxBufferItems * entitiesToMove;
                                    srcPtr = metaData[i].bufferPtr + (metaData[i].bufferItemTypeSize * metaData[i].maxBufferItems * (index + 1));
                                    dstPtr = metaData[i].bufferPtr + (metaData[i].bufferItemTypeSize * metaData[i].maxBufferItems * index);
                                    Buffer.MemoryCopy((void*)srcPtr, (void*)dstPtr, srcBytes, srcBytes);
                                }
                            }

                            for (int i = 0; i < componentCount; i++)
                            {
                                if (metaData[i].isBuffer)
                                {
                                    // Reset component buffer handles
                                    Container<int>* pHandle = (Container<int>*)metaData[i].compPtr;
                                    for (int j = 0; j < maxEntityCount; j++)
                                    {
                                        pHandle[j].SetBufferPtr(metaData[i].bufferPtr + (metaData[i].bufferItemTypeSize * metaData[i].maxBufferItems * j));
                                    }
                                }
                            }
                        }
                        entityCount--;
                    }
                }
            }

            public ComponentBuffer(int maxEntities)
            {
                metaData = new ComponentMetadata[128];
                maxEntityCount = maxEntities;
                RegisterComponent<Entity>();
            }

            public void AlignTo64(ref int allocation)
            {
                int mod = allocation % 64;
                if (mod != 0)
                {
                    int diff = 64 - mod;
                    allocation += diff;
                }
            }

            public void Allocate()
            {
                int allocationSize = 0;
                for (int i = 0; i < componentCount; i++)
                {
                    // Reserve space for components 
                    allocationSize += metaData[i].typeSize * maxEntityCount;
                    AlignTo64(ref allocationSize);

                    // Reserve space for component buffers 
                    if (metaData[i].isBuffer)
                    {
                        allocationSize += metaData[i].bufferItemTypeSize * metaData[i].maxBufferItems * maxEntityCount;
                        AlignTo64(ref allocationSize);
                    }
                }

                compsPtr = (IntPtr)NativeMemory.AlignedAlloc((nuint)allocationSize, 64);

                hasAllocated = true;


                // Set component metadata 
                allocationSize = 0;
                for (int i = 0; i < componentCount; i++)
                {
                    metaData[i].compPtr = (IntPtr)(compsPtr + allocationSize);

                    allocationSize += metaData[i].typeSize * maxEntityCount;
                    AlignTo64(ref allocationSize);

                    if (metaData[i].isBuffer)
                    {
                        metaData[i].bufferPtr = (IntPtr)(compsPtr + allocationSize);
                        allocationSize += metaData[i].bufferItemTypeSize * metaData[i].maxBufferItems * maxEntityCount;
                        AlignTo64(ref allocationSize);

                        // Set component buffer handles
                        Container<int>* pHandle = (Container<int>*)metaData[i].compPtr;
                        IntPtr pBuffer = metaData[i].bufferPtr;
                        for (int j = 0; j < maxEntityCount; j++)
                        {
                            pHandle[j] = new Container<int>(pBuffer + (metaData[i].bufferItemTypeSize * metaData[i].maxBufferItems * j), 0, metaData[i].maxBufferItems);
                        }
                    }
                }

            }

            public void RegisterComponent<T>()
            where T : unmanaged
            {
                ComponentMetadata componentMetadata = default;

                componentMetadata.isBuffer = false;
                componentMetadata.type = typeof(T).TypeHandle.Value.ToInt64();
                componentMetadata.typeSize = sizeof(T);

                metaData[componentCount++] = componentMetadata;
            }

            public void RegisterComponentBuffer<T>(int numSlots)
            where T : unmanaged
            {
                ComponentMetadata componentMetadata = default;

                componentMetadata.isBuffer = true;
                componentMetadata.type = typeof(Container<T>).TypeHandle.Value.ToInt64();
                componentMetadata.typeSize = sizeof(Container<T>);
                componentMetadata.bufferItemType = typeof(T).TypeHandle.Value.ToInt64();
                componentMetadata.bufferItemTypeSize = sizeof(T);
                componentMetadata.maxBufferItems = numSlots;

                metaData[componentCount++] = componentMetadata;
            }

            public Entity CreateEntity()
            {
                if (entityCount < maxEntityCount)
                {
                    entityCount++;
                    GetFromIndex(entityCount - 1, out Entity* id);
                    id->entityID = currentEntityID++;
                    return new Entity { entityID = (ushort)(entityCount - 1) };
                }
                return new Entity { entityID = 0 };
            }

            public bool GetFromIndex<T>(int index, out T* t) 
            where T : unmanaged
            {
                T* pComponent = GetBuffer<T>();

                if (pComponent != null)
                {
                    t = &pComponent[index];
                    return true;
                }

                t = null;
                return false;
            }

            public T* GetBuffer<T>() 
            where T : unmanaged
            {
                T* pComponent = null;
                for (int i = 0; i < componentCount; i++)
                {
                    if (metaData[i].type == typeof(T).TypeHandle.Value.ToInt64())
                    {
                        pComponent = (T*)metaData[i].compPtr;
                        break;
                    }
                }
                return pComponent;
            }

            public void ForEach<T1>(ForEachDelegate<T1> scalarInvoke)
            where T1 : unmanaged
            {
                Entity* pEntity = GetBuffer<Entity>();
                T1* t1 = GetBuffer<T1>();

                for (int i = 0; i < entityCount; i++) {
                    scalarInvoke(ref pEntity[i], ref t1[i]); 
                }
            }

            public void ForEach<T1, T2>(ForEachDelegate<T1, T2> scalarInvoke)
            where T1 : unmanaged
            where T2 : unmanaged
            {
                Entity* pEntity = GetBuffer<Entity>();
                T1* t1 = GetBuffer<T1>();
                T2* t2 = GetBuffer<T2>();

                for (int i = 0; i < entityCount; i++) {
                    scalarInvoke(ref pEntity[i], ref t1[i], ref t2[i]); 
                }
            }

            public void ForEach<T1, T2, T3>(ForEachDelegate<T1, T2, T3> scalarInvoke)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            {
                Entity* pEntity = GetBuffer<Entity>();
                T1* t1 = GetBuffer<T1>();
                T2* t2 = GetBuffer<T2>();
                T3* t3 = GetBuffer<T3>();

                for (int i = 0; i < entityCount; i++)
                {
                    scalarInvoke(ref pEntity[i], ref t1[i], ref t2[i], ref t3[i]);
                }
            }

            public void ForEachBatch<T1>(int batchSize, ForEachDelegateBatch<T1> batchInvoke, ForEachDelegate<T1> scalarInvoke)
            where T1 : unmanaged
            {
                Entity* pEntity = GetBuffer<Entity>();
                T1* t1 = GetBuffer<T1>();

                int batchAffected = (entityCount / batchSize) * batchSize;
                for (int i = 0; i < batchAffected; i += batchSize)
                {
                    batchInvoke(&pEntity[i], &t1[i]);
                }

                for (int i = batchAffected; i < entityCount; i++)
                {
                    scalarInvoke(ref pEntity[i], ref t1[i]);
                }
            }

            public void ForEachBatch<T1, T2>(int batchSize, ForEachDelegateBatch<T1, T2> batchInvoke, ForEachDelegate<T1, T2> scalarInvoke)
            where T1 : unmanaged
            where T2 : unmanaged
            {
                Entity* pEntity = GetBuffer<Entity>();
                T1* t1 = GetBuffer<T1>();
                T2* t2 = GetBuffer<T2>();

                int batchAffected = (entityCount / batchSize) * batchSize;
                for (int i = 0; i < batchAffected; i += batchSize)
                {
                    batchInvoke(&pEntity[i], &t1[i], &t2[i]);
                }

                for (int i = batchAffected; i < entityCount; i++)
                {
                    scalarInvoke(ref pEntity[i], ref t1[i], ref t2[i]);
                }
            }

            public void ForEachBatch<T1, T2, T3>(int batchSize, ForEachDelegateBatch<T1, T2, T3> batchInvoke, ForEachDelegate<T1, T2, T3> scalarInvoke)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
            {
                Entity* pEntity = GetBuffer<Entity>();
                T1* t1 = GetBuffer<T1>();
                T2* t2 = GetBuffer<T2>();
                T3* t3 = GetBuffer<T3>();

                int batchAffected = (entityCount / batchSize) * batchSize;
                for (int i = 0; i < batchAffected; i += batchSize)
                {
                    batchInvoke(&pEntity[i], &t1[i], &t2[i], &t3[i]);
                }

                for (int i = batchAffected; i < entityCount; i++)
                {
                    scalarInvoke(ref pEntity[i], ref t1[i], ref t2[i], ref t3[i]);
                }
            }

        }

        public delegate void ForEachDelegate<T1>(ref Entity entity, ref T1 t1);
        public delegate void ForEachDelegate<T1, T2>(ref Entity entity, ref T1 t1, ref T2 t2);
        public delegate void ForEachDelegate<T1, T2, T3>(ref Entity entity, ref T1 t1, ref T2 t2, ref T3 t3);
        public delegate void ForEachDelegateBatch<T1>(Entity* entity, T1* t1);
        public delegate void ForEachDelegateBatch<T1, T2>(Entity* entity, T1* t1, T2* t2);
        public delegate void ForEachDelegateBatch<T1, T2, T3>(Entity* entity, T1* t1, T2* t2, T3* t3);
    }
}
