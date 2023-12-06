# CSDataOrientedBuffer
Demonstrates the overall performance of Data Oriented Programming vs Object Oriented Programming while operating on large data sets.
Unfinished code.

## Usage

### Setup
#### Initialize buffer and set maximum entity capacity
```
int entityCapacity = 50000;
ComponentBuffer componentBuffer = new ComponentBuffer(entityCapacity);
```
#### Register buffer components. Must be of type struct
```
public struct PositionX { public float value; }
public struct PositionY { public float value; }
public struct PositionZ { public float value; }

componentBuffer.RegisterComponent<PositionX>();
componentBuffer.RegisterComponent<PositionY>();
componentBuffer.RegisterComponent<PositionZ>();
```
#### Create entities
```
for (int i = 0; i < 50000; i++) {
  Entity entityHandle = componentBuffer.CreateEntity();
}
```
#### Operate on data
##### Scalar operations
```
componentBuffer.ForEach(
static (ref Entity entity, ref PositionX posX, ref PositionY posY, ref PositionZ posZ) =>
{
  posX.value = 1;
  posY.value = 2;
  posZ.value = 3;
});
```
##### Batched Operations. SIMD or manual unrolling
##### All internal component buffers are allocated with a 64 byte alignment
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

## Benchmark
### Linear interpolation between two 3D vectors
#### Operating on 50,000 entities
##### i7 6700k
##### .Net 8
##### BenchmarkDotNet

<br>

#### Setup
```
public class oopClass
{
  public uint networkID;
  public uint networkAddress;
  public ushort networkPort;
  public float positionX;
  public float positionY;
  public float positionZ;
  public float[] someBuffer;

  public oopClass()
  {
    someBuffer = new float[64];
  }
}

public struct PositionX { public float value; }
public struct PositionY { public float value; }
public struct PositionZ { public float value; }

public int entityAmount;
public oopClass[] oopBuffer;
public ComponentBuffer componentBuffer;

[GlobalSetup]
public void GlobalSetup()
{
  entityAmount = 50000;
  componentBuffer = new ComponentBuffer(entityAmount);
  componentBuffer.RegisterComponent<PositionX>();
  componentBuffer.RegisterComponent<PositionY>();
  componentBuffer.RegisterComponent<PositionZ>();
  componentBuffer.Allocate();
  oopBuffer = new oopClass[entityAmount];
  for (int i = 0; i < entityAmount; i++) { componentBuffer.CreateEntity(); }
  for (int i = 0; i < oopBuffer.Length; i++) { oopBuffer[i] = new oopClass(); }
}
```

#### Tests
```
[Benchmark]
public void TestOOP()
{
  for (int i = 0; i < oopBuffer.Length; i++)
  {
    float alpha = 0.75f;
    float inverseAlpha = 1f - 0.75f;

    float posXTarget = 5f;
    float posYTarget = 10f;
    float posZTarget = 15f;
    oopBuffer[i].positionX = (oopBuffer[i].positionX * inverseAlpha) + (posXTarget * alpha);
    oopBuffer[i].positionY = (oopBuffer[i].positionY * inverseAlpha) + (posYTarget * alpha);
    oopBuffer[i].positionZ = (oopBuffer[i].positionZ * inverseAlpha) + (posZTarget * alpha);
  }
}

[Benchmark]
public void TestDOP()
{
  componentBuffer.ForEach(static (ref Entity entity, ref PositionX posX, ref PositionY posY, ref PositionZ posZ) =>
  {
    float alpha = 0.75f;
    float inverseAlpha = 1f - 0.75f;

    float posXTarget = 5f;
    float posYTarget = 10f;
    float posZTarget = 15f;

    float firstPartX = posX.value * inverseAlpha;
    float firstPartY = posY.value * inverseAlpha;
    float firstPartZ = posZ.value * inverseAlpha;

    float secondPartX = posXTarget * alpha;
    float secondPartY = posYTarget * alpha;
    float secondPartZ = posZTarget * alpha;

    posX.value = firstPartX + secondPartX;
    posY.value = firstPartY + secondPartY;
    posZ.value = firstPartZ + secondPartZ;
  });
}

[Benchmark]
public void TestDOPUnrolled()
{
  componentBuffer.ForEachBatch(2, static (Entity* entity, PositionX* posX, PositionY* posY, PositionZ* posZ) =>
  {
    float alpha1 = 0.75f;
    float inverseAlpha1 = 1f - 0.75f;

    float posXTarget1 = 5f;
    float posYTarget1 = 10f;
    float posZTarget1 = 15f;

    float firstPartX1 = posX[0].value * inverseAlpha1;
    float firstPartY1 = posY[0].value * inverseAlpha1;
    float firstPartZ1 = posZ[0].value * inverseAlpha1;

    float secondPartX1 = posXTarget1 * alpha1;
    float secondPartY1 = posYTarget1 * alpha1;
    float secondPartZ1 = posZTarget1 * alpha1;

    float firstPartX2 = posX[1].value * inverseAlpha1;
    float firstPartY2 = posY[1].value * inverseAlpha1;
    float firstPartZ2 = posZ[1].value * inverseAlpha1;

    float secondPartX2 = posXTarget1 * alpha1;
    float secondPartY2 = posYTarget1 * alpha1;
    float secondPartZ2 = posZTarget1 * alpha1;

    posX[0].value = firstPartX1 + secondPartX1;
    posY[0].value = firstPartY1 + secondPartY1;
    posZ[0].value = firstPartZ1 + secondPartZ1;

    posX[1].value = firstPartX2 + secondPartX2;
    posY[1].value = firstPartY2 + secondPartY2;
    posZ[1].value = firstPartZ2 + secondPartZ2;
  }, 
  (ref Entity entity, ref PositionX posX, ref PositionY posY, ref PositionZ posZ) =>
  {
    // scalar overflow
  });
}

[Benchmark]
public void TestDOPSIMD()
{
  componentBuffer.ForEachBatch(8,
  static (Entity* entity, PositionX* posX, PositionY* posY, PositionZ* posZ) =>
  {
    Vector256<float> vPosX = Avx.LoadAlignedVector256((float*)posX);
    Vector256<float> vPosY = Avx.LoadAlignedVector256((float*)posY);
    Vector256<float> vPosZ = Avx.LoadAlignedVector256((float*)posZ);

    Vector256<float> vAlpha        = Vector256.Create(0.75f);
    Vector256<float> vInverseAlpha = Vector256.Create(1f - 0.75f);

    Vector256<float> vPosXTarget = Vector256.Create(5f);
    Vector256<float> vPosYTarget = Vector256.Create(10f);
    Vector256<float> vPosZTarget = Vector256.Create(15f);

    Vector256<float> firstPartX = Avx.Multiply(vPosX, vInverseAlpha);
    Vector256<float> firstPartY = Avx.Multiply(vPosY, vInverseAlpha);
    Vector256<float> firstPartZ = Avx.Multiply(vPosZ, vInverseAlpha);

    Vector256<float> secondPartX = Avx.Multiply(vPosXTarget, vAlpha);
    Vector256<float> secondPartY = Avx.Multiply(vPosYTarget, vAlpha);
    Vector256<float> secondPartZ = Avx.Multiply(vPosZTarget, vAlpha);

    Vector256<float> resultX = Avx.Add(firstPartX, secondPartX);
    Vector256<float> resultY = Avx.Add(firstPartY, secondPartY);
    Vector256<float> resultZ = Avx.Add(firstPartZ, secondPartZ);

    Avx.StoreAligned((float*)posX, resultX);
    Avx.StoreAligned((float*)posX, resultY);
    Avx.StoreAligned((float*)posX, resultZ);
  },
  (ref Entity entity, ref PositionX posX, ref PositionY posY, ref PositionZ posZ) =>
  {
    // scalar overflow
  });
}
```

#### Results
```
| Method          | Mean      | Error    | StdDev   |
|---------------- |----------:|---------:|---------:|
| TestOOP         | 325.22 us | 6.331 us | 5.612 us |
| TestDOP         |  85.13 us | 0.341 us | 0.302 us |
| TestDOPUnrolled |  50.96 us | 0.783 us | 0.654 us |
| TestDOPSIMD     |  11.90 us | 0.154 us | 0.129 us |
```
