using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Assertions;
using UnityEngine.Jobs;

namespace UnityEngine.Rendering.Universal
{
    internal class DecalEntityIndexer
    {
        public struct DecalEntityItem
        {
            public int chunkIndex;
            public int arrayIndex;
            public int version;
        }

        private List<DecalEntityItem> entities = new List<DecalEntityItem>();
        private Queue<int> unused = new Queue<int>();

        public bool IsValid(DecalEntity decalEntity)
        {
            if (entities.Count <= decalEntity.index)
                return false;

            return entities[decalEntity.index].version == decalEntity.version;
        }

        public DecalEntity CreateDecalEntity(int arrayIndex, int chunkIndex)
        {
            // Reuse
            if (unused.Count != 0)
            {
                int entityIndex = unused.Dequeue();
                int newVersion = entities[entityIndex].version + 1;

                entities[entityIndex] = new DecalEntityItem()
                {
                    arrayIndex = arrayIndex,
                    chunkIndex = chunkIndex,
                    version = newVersion,
                };

                return new DecalEntity()
                {
                    index = entityIndex,
                    version = newVersion,
                };
            }

            // Create new one
            {
                int entityIndex = entities.Count;
                int version = 1;

                entities.Add(new DecalEntityItem()
                {
                    arrayIndex = arrayIndex,
                    chunkIndex = chunkIndex,
                    version = version,
                });


                return new DecalEntity()
                {
                    index = entityIndex,
                    version = version,
                };
            }
        }

        public void DestroyDecalEntity(DecalEntity decalEntity)
        {
            Assert.IsTrue(IsValid(decalEntity));
            unused.Enqueue(decalEntity.index);

            // Update version that everything that points to it will have oudated version
            var item = entities[decalEntity.index];
            item.version++;
            entities[decalEntity.index] = item;
        }

        public DecalEntityItem GetItem(DecalEntity decalEntity)
        {
            Assert.IsTrue(IsValid(decalEntity));
            return entities[decalEntity.index];
        }

        public void UpdateIndex(DecalEntity decalEntity, int newArrayIndex)
        {
            Assert.IsTrue(IsValid(decalEntity));
            var item = entities[decalEntity.index];
            item.arrayIndex = newArrayIndex;
            item.version = decalEntity.version;
            entities[decalEntity.index] = item;
        }

        public void RemapChunkIndices(List<int> remaper)
        {
            for (int i = 0; i < entities.Count; ++i)
            {
                int newChunkIndex = remaper[entities[i].chunkIndex];
                var item = entities[i];
                item.chunkIndex = newChunkIndex;
                entities[i] = item;
            }
        }

        public void Clear()
        {
            entities.Clear();
            unused.Clear();
        }
    }

    internal struct DecalEntity
    {
        public int index;
        public int version;
    }

    internal class DecalEntityChunk : DecalChunk
    {
        public Material material;
        public NativeArray<DecalEntity> decalEntities;
        public DecalProjector[] decalProjectors;
        public TransformAccessArray transformAccessArray;

        public override void Push()
        {
            count++;
        }

        public override void RemoveAtSwapBack(int entityIndex)
        {
            RemoveAtSwapBack(ref decalEntities, entityIndex, count);
            RemoveAtSwapBack(ref decalProjectors, entityIndex, count);
            transformAccessArray.RemoveAtSwapBack(entityIndex);
            count--;
        }

        public override void SetCapacity(int newCapacity)
        {
            ResizeNativeArray(ref decalEntities, newCapacity);
            ResizeNativeArray(ref transformAccessArray, decalProjectors, newCapacity);
            ResizeArray(ref decalProjectors, newCapacity);
            capacity = newCapacity;
        }

        public override void Dispose()
        {
            if (capacity == 0)
                return;

            decalEntities.Dispose();
            transformAccessArray.Dispose();
            decalProjectors = null;
            count = 0;
            capacity = 0;
        }
    }

    internal class DecalEntityManager : IDisposable
    {
        public List<DecalEntityChunk> entityChunks = new List<DecalEntityChunk>();
        public List<DecalCachedChunk> cachedChunks = new List<DecalCachedChunk>();
        public List<DecalCulledChunk> culledChunks = new List<DecalCulledChunk>();
        public List<DecalDrawCallChunk> drawCallChunks = new List<DecalDrawCallChunk>();
        public int chunkCount;

        private ProfilingSampler m_AddDecalSampler;
        private ProfilingSampler m_ResizeChunks;
        private ProfilingSampler m_SortChunks;

        private DecalEntityIndexer m_DecalEntityIndexer = new DecalEntityIndexer();
        private Dictionary<Material, int> m_MaterialToChunkIndex = new Dictionary<Material, int>();

        private struct CombinedChunks
        {
            public DecalEntityChunk entityChunk;
            public DecalCachedChunk cachedChunk;
            public DecalCulledChunk culledChunk;
            public DecalDrawCallChunk drawCallChunk;
            public int previousChunkIndex;
        }
        private List<CombinedChunks> m_CombinedChunks = new List<CombinedChunks>();
        private List<int> m_CombinedChunkRemmap = new List<int>();

        private Material m_ErrorMaterial;
        public Material errorMaterial => m_ErrorMaterial;

        public DecalEntityManager()
        {
            m_AddDecalSampler = new ProfilingSampler("DecalEntityManager.CreateDecalEntity");
            m_ResizeChunks = new ProfilingSampler("DecalEntityManager.ResizeChunks");
            m_SortChunks = new ProfilingSampler("DecalEntityManager.SortChunks");
            m_ErrorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }

        public bool IsValid(DecalEntity decalEntity)
        {
            return m_DecalEntityIndexer.IsValid(decalEntity);
        }

        public DecalEntity CreateDecalEntity(DecalProjector decalProjector)
        {
            var material = decalProjector.material;
            if (material == null)
                material = errorMaterial;

            using (new ProfilingScope(null, m_AddDecalSampler))
            {
                int chunkIndex = CreateChunkIndex(material);
                int entityIndex = entityChunks[chunkIndex].count;

                DecalEntity entity = m_DecalEntityIndexer.CreateDecalEntity(entityIndex, chunkIndex);

                DecalEntityChunk entityChunk = entityChunks[chunkIndex];
                DecalCachedChunk cachedChunk = cachedChunks[chunkIndex];
                DecalCulledChunk culledChunk = culledChunks[chunkIndex];
                DecalDrawCallChunk drawCallChunk = drawCallChunks[chunkIndex];

                // Make sure we have space to add new entity
                if (entityChunks[chunkIndex].capacity == entityChunks[chunkIndex].count)
                {
                    using (new ProfilingScope(null, m_ResizeChunks))
                    {
                        int newCapacity = entityChunks[chunkIndex].capacity + entityChunks[chunkIndex].capacity;
                        newCapacity = math.max(250, newCapacity);

                        entityChunk.SetCapacity(newCapacity);
                        cachedChunk.SetCapacity(newCapacity);
                        culledChunk.SetCapacity(newCapacity);
                        drawCallChunk.SetCapacity(newCapacity);
                    }
                }

                entityChunk.Push();
                cachedChunk.Push();
                culledChunk.Push();
                drawCallChunk.Push();

                entityChunk.decalProjectors[entityIndex] = decalProjector;
                entityChunk.decalEntities[entityIndex] = entity;
                entityChunk.transformAccessArray.Add(decalProjector.transform);

                UpdateDecalEntityData(entity, decalProjector);

                return entity;
            }
        }

        private int CreateChunkIndex(Material material)
        {
            if (!m_MaterialToChunkIndex.TryGetValue(material, out int chunkIndex))
            {
                entityChunks.Add(new DecalEntityChunk() { material = material });
                cachedChunks.Add(new DecalCachedChunk()
                {
                    propertyBlock = new MaterialPropertyBlock(),
                    drawDistance = 1000,
                });

                culledChunks.Add(new DecalCulledChunk());
                drawCallChunks.Add(new DecalDrawCallChunk() { subCallCounts = new NativeArray<int>(1, Allocator.Persistent) });

                m_CombinedChunks.Add(new CombinedChunks());
                m_CombinedChunkRemmap.Add(0);

                m_MaterialToChunkIndex.Add(material, chunkCount);
                return chunkCount++;
            }

            return chunkIndex;
        }

        public void UpdateDecalEntityData(DecalEntity decalEntity, DecalProjector decalProjector)
        {
            var decalItem = m_DecalEntityIndexer.GetItem(decalEntity);

            int chunkIndex = decalItem.chunkIndex;
            int arrayIndex = decalItem.arrayIndex;

            DecalCachedChunk cachedChunk = cachedChunks[chunkIndex];

            cachedChunk.sizeOffsets[arrayIndex] = Matrix4x4.Translate(decalProjector.decalOffset) * Matrix4x4.Scale(decalProjector.decalSize);

            float drawDistance = decalProjector.drawDistance;
            float fadeScale = decalProjector.fadeScale;
            float startAngleFade = decalProjector.startAngleFade;
            float endAngleFade = decalProjector.endAngleFade;
            Vector4 uvScaleBias = decalProjector.uvScaleBias;
            bool affectsTransparency = decalProjector.affectsTransparency;
            int layerMask = decalProjector.gameObject.layer;
            ulong sceneLayerMask = decalProjector.gameObject.sceneCullingMask;
            float fadeFactor = decalProjector.fadeFactor;
            DecalLayerEnum decalLayerMask = decalProjector.decalLayerMask;

            // draw distance can't be more than global draw distance
            cachedChunk.drawDistances[arrayIndex] = new Vector2(cachedChunk.drawDistance < drawDistance
                ? drawDistance
                : cachedChunk.drawDistance, fadeScale);
            // In the shader to remap from cosine -1 to 1 to new range 0..1  (with 0 - 0 degree and 1 - 180 degree)
            // we do 1.0 - (dot() * 0.5 + 0.5) => 0.5 * (1 - dot())
            // we actually square that to get smoother result => x = (0.5 - 0.5 * dot())^2
            // Do a remap in the shader. 1.0 - saturate((x - start) / (end - start))
            // After simplification => saturate(a + b * dot() * (dot() - 2.0))
            // a = 1.0 - (0.25 - start) / (end - start), y = - 0.25 / (end - start)
            if (startAngleFade == 180.0f) // angle fade is disabled
            {
                cachedChunk.angleFades[arrayIndex] = new Vector2(0.0f, 0.0f);
            }
            else
            {
                float angleStart = startAngleFade / 180.0f;
                float angleEnd = endAngleFade / 180.0f;
                var range = Mathf.Max(0.0001f, angleEnd - angleStart);
                cachedChunk.angleFades[arrayIndex] = new Vector2(1.0f - (0.25f - angleStart) / range, -0.25f / range);
            }
            cachedChunk.uvScaleBias[arrayIndex] = uvScaleBias;
            cachedChunk.affectsTransparencies[arrayIndex] = affectsTransparency;
            cachedChunk.layerMasks[arrayIndex] = layerMask;
            cachedChunk.sceneLayerMasks[arrayIndex] = sceneLayerMask;
            cachedChunk.fadeFactors[arrayIndex] = fadeFactor;
            cachedChunk.decalLayerMasks[arrayIndex] = decalLayerMask;
            cachedChunk.dirty[arrayIndex] = true;
        }

        public void DestroyDecalEntity(DecalEntity decalEntity)
        {
            if (!m_DecalEntityIndexer.IsValid(decalEntity))
                return;

            var decalItem = m_DecalEntityIndexer.GetItem(decalEntity);
            m_DecalEntityIndexer.DestroyDecalEntity(decalEntity);

            int chunkIndex = decalItem.chunkIndex;
            int arrayIndex = decalItem.arrayIndex;

            DecalEntityChunk entityChunk = entityChunks[chunkIndex];
            DecalCachedChunk cachedChunk = cachedChunks[chunkIndex];
            DecalCulledChunk culledChunk = culledChunks[chunkIndex];
            DecalDrawCallChunk drawCallChunk = drawCallChunks[chunkIndex];

            int lastArrayIndex = entityChunk.count - 1;
            if (arrayIndex != lastArrayIndex)
                m_DecalEntityIndexer.UpdateIndex(entityChunk.decalEntities[lastArrayIndex], arrayIndex);

            entityChunk.RemoveAtSwapBack(arrayIndex);
            cachedChunk.RemoveAtSwapBack(arrayIndex);
            culledChunk.RemoveAtSwapBack(arrayIndex);
            drawCallChunk.RemoveAtSwapBack(arrayIndex);
        }

        public void Sort()
        {
            using (new ProfilingScope(null, m_SortChunks))
            {
                // Combine chunks into single array
                for (int i = 0; i < chunkCount; ++i)
                {
                    m_CombinedChunks[i] = new CombinedChunks()
                    {
                        entityChunk = entityChunks[i],
                        cachedChunk = cachedChunks[i],
                        culledChunk = culledChunks[i],
                        drawCallChunk = drawCallChunks[i],
                        previousChunkIndex = i,
                    };
                }

                // Sort
                m_CombinedChunks.Sort((a, b) =>
                {
                    if (a.cachedChunk.drawOrder < b.cachedChunk.drawOrder)
                        return -1;
                    if (a.cachedChunk.drawOrder > b.cachedChunk.drawOrder)
                        return 1;
                    return a.GetHashCode().CompareTo(b.GetHashCode());
                });

                // Early out if nothing changed
                bool dirty = false;
                for (int i = 0; i < chunkCount; ++i)
                {
                    if (m_CombinedChunks[i].previousChunkIndex != i)
                    {
                        dirty = true;
                        break;
                    }
                }
                if (!dirty)
                    return;

                // Update chunks
                m_MaterialToChunkIndex.Clear();
                for (int i = 0; i < chunkCount; ++i)
                {
                    entityChunks[i] = m_CombinedChunks[i].entityChunk;
                    cachedChunks[i] = m_CombinedChunks[i].cachedChunk;
                    culledChunks[i] = m_CombinedChunks[i].culledChunk;
                    drawCallChunks[i] = m_CombinedChunks[i].drawCallChunk;
                    m_MaterialToChunkIndex.Add(entityChunks[i].material, i);
                    m_CombinedChunkRemmap[m_CombinedChunks[i].previousChunkIndex] = i;
                }

                // Remap entities chunk index with new sorted ones
                m_DecalEntityIndexer.RemapChunkIndices(m_CombinedChunkRemmap);
            }
        }

        public void Dispose()
        {
            foreach (var entityChunk in entityChunks)
                entityChunk.currentJobHandle.Complete();
            foreach (var cachedChunk in cachedChunks)
                cachedChunk.currentJobHandle.Complete();
            foreach (var culledChunk in culledChunks)
                culledChunk.currentJobHandle.Complete();
            foreach (var drawCallChunk in drawCallChunks)
                drawCallChunk.currentJobHandle.Complete();

            foreach (var entityChunk in entityChunks)
                entityChunk.Dispose();
            foreach (var cachedChunk in cachedChunks)
                cachedChunk.Dispose();
            foreach (var culledChunk in culledChunks)
                culledChunk.Dispose();
            foreach (var drawCallChunk in drawCallChunks)
                drawCallChunk.Dispose();

            m_DecalEntityIndexer.Clear();
            m_MaterialToChunkIndex.Clear();
            entityChunks.Clear();
            cachedChunks.Clear();
            culledChunks.Clear();
            drawCallChunks.Clear();
            m_CombinedChunks.Clear();
            chunkCount = 0;
        }
    }
}
