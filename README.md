# CSDataOrientedBuffer
Demonstrates the overall performance of Data Oriented Programming vs Object Oriented Programming while operating on large data sets.
Unfinished code.

## Usage

### Setup
#### Initialize buffer and set maximum entity capacity.
```
int entityCapacity = 50000;
ComponentBuffer componentBuffer = new ComponentBuffer(entityCapacity);
```
#### Register buffer components. Must be of type struct. 
```
public struct PositionX { public float value; }
public struct PositionY { public float value; }
public struct PositionZ { public float value; }

componentBuffer.RegisterComponent<PositionX>();
componentBuffer.RegisterComponent<PositionY>();
componentBuffer.RegisterComponent<PositionZ>();
```
#### Create entities. 
```
for (int i = 0; i < 50000; i++) {
  Entity entityHandle = componentBuffer.CreateEntity();
}
```
#### Operate on data.
##### Scalar operations.
```
componentBuffer.ForEach(
static (ref Entity entity, ref PositionX posX, ref PositionY posY, ref PositionZ posZ) =>
{
  posX.value = 1;
  posY.value = 2;
  posZ.value = 3;
});
```
##### Batched Operations. SIMD or manual unrolling. 
##### All internal component buffers are allocated with a 64 byte alignment.
```
componentBuffer.ForEachBatch(8,
static (Entity* entity, PositionX* posX, PositionY* posY, PositionZ* posZ) =>
{
  Vector256<float> vPosX = Avx.LoadAlignedVector256((float*)posX);
  Vector256<float> vPosY = Avx.LoadAlignedVector256((float*)posY);
  Vector256<float> vPosZ = Avx.LoadAlignedVector256((float*)posZ);

  Vector256<float> vAddX = Vector256.Create<float>(1);
  Vector256<float> vAddY = Vector256.Create<float>(2);
  Vector256<float> vAddZ = Vector256.Create<float>(3);

  Vector256<float> vResultX = Avx.Add(vPosX, vAddX);
  Vector256<float> vResultY = Avx.Add(vPosY, vAddY);
  Vector256<float> vResultZ = Avx.Add(vPosZ, vAddZ);

  Avx.StoreAligned((float*)posX, vResultX);
  Avx.StoreAligned((float*)posY, vResultY);
  Avx.StoreAligned((float*)posZ, vResultZ);
},

(ref Entity entity, ref PositionX posX, ref PositionY posY, ref PositionZ posZ) =>
{
  // scalar overflow
});
```
