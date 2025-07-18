// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Memory.Allocators;

/// <summary>
/// Inherit this class to test an <see cref="IMemoryOwner{T}"/> implementation (provided by <see cref="MemoryAllocator"/>).
/// </summary>
public abstract class BufferTestSuite
{
    protected BufferTestSuite(MemoryAllocator memoryAllocator)
    {
        this.MemoryAllocator = memoryAllocator;
    }

    protected MemoryAllocator MemoryAllocator { get; }

    public struct CustomStruct : IEquatable<CustomStruct>
    {
        public long A;

        public byte B;

        public float C;

        public CustomStruct(long a, byte b, float c)
        {
            this.A = a;
            this.B = b;
            this.C = c;
        }

        public bool Equals(CustomStruct other)
        {
            return this.A == other.A && this.B == other.B && this.C.Equals(other.C);
        }

        public override bool Equals(object obj)
        {
            return obj is CustomStruct other && this.Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.A.GetHashCode();
                hashCode = (hashCode * 397) ^ this.B.GetHashCode();
                hashCode = (hashCode * 397) ^ this.C.GetHashCode();
                return hashCode;
            }
        }
    }

    public static readonly TheoryData<int> LengthValues = new() { 0, 1, 7, 1023, 1024 };

    [Theory]
    [MemberData(nameof(LengthValues))]
    public void HasCorrectLength_byte(int desiredLength)
    {
        this.TestHasCorrectLength<byte>(desiredLength);
    }

    [Theory]
    [MemberData(nameof(LengthValues))]
    public void HasCorrectLength_float(int desiredLength)
    {
        this.TestHasCorrectLength<float>(desiredLength);
    }

    [Theory]
    [MemberData(nameof(LengthValues))]
    public void HasCorrectLength_CustomStruct(int desiredLength)
    {
        this.TestHasCorrectLength<CustomStruct>(desiredLength);
    }

    private void TestHasCorrectLength<T>(int desiredLength)
        where T : struct
    {
        using (IMemoryOwner<T> buffer = this.MemoryAllocator.Allocate<T>(desiredLength))
        {
            Assert.Equal(desiredLength, buffer.GetSpan().Length);
        }
    }

    [Theory]
    [MemberData(nameof(LengthValues))]
    public void CanAllocateCleanBuffer_byte(int desiredLength)
    {
        this.TestCanAllocateCleanBuffer<byte>(desiredLength);
    }

    [Theory]
    [MemberData(nameof(LengthValues))]
    public void CanAllocateCleanBuffer_double(int desiredLength)
    {
        this.TestCanAllocateCleanBuffer<double>(desiredLength);
    }

    [Theory]
    [MemberData(nameof(LengthValues))]
    public void CanAllocateCleanBuffer_CustomStruct(int desiredLength)
    {
        this.TestCanAllocateCleanBuffer<CustomStruct>(desiredLength);
    }

    private void TestCanAllocateCleanBuffer<T>(int desiredLength)
        where T : struct, IEquatable<T>
    {
        ReadOnlySpan<T> expected = new T[desiredLength];

        for (int i = 0; i < 10; i++)
        {
            using (IMemoryOwner<T> buffer = this.MemoryAllocator.Allocate<T>(desiredLength, AllocationOptions.Clean))
            {
                Assert.True(buffer.GetSpan().SequenceEqual(expected));
            }
        }
    }

    [Theory]
    [MemberData(nameof(LengthValues))]
    public void SpanPropertyIsAlwaysTheSame_int(int desiredLength)
    {
        this.TestSpanPropertyIsAlwaysTheSame<int>(desiredLength);
    }

    [Theory]
    [MemberData(nameof(LengthValues))]
    public void SpanPropertyIsAlwaysTheSame_byte(int desiredLength)
    {
        this.TestSpanPropertyIsAlwaysTheSame<byte>(desiredLength);
    }

    private void TestSpanPropertyIsAlwaysTheSame<T>(int desiredLength)
        where T : struct
    {
        using (IMemoryOwner<T> buffer = this.MemoryAllocator.Allocate<T>(desiredLength, AllocationOptions.None))
        {
            ref T a = ref MemoryMarshal.GetReference(buffer.GetSpan());
            ref T b = ref MemoryMarshal.GetReference(buffer.GetSpan());
            ref T c = ref MemoryMarshal.GetReference(buffer.GetSpan());

            Assert.True(Unsafe.AreSame(ref a, ref b));
            Assert.True(Unsafe.AreSame(ref b, ref c));
        }
    }

    [Theory]
    [MemberData(nameof(LengthValues))]
    public void WriteAndReadElements_float(int desiredLength)
    {
        this.TestWriteAndReadElements(desiredLength, x => x * 1.2f);
    }

    [Theory]
    [MemberData(nameof(LengthValues))]
    public void WriteAndReadElements_byte(int desiredLength)
    {
        this.TestWriteAndReadElements(desiredLength, x => (byte)(x + 1));
    }

    private void TestWriteAndReadElements<T>(int desiredLength, Func<int, T> getExpectedValue)
        where T : struct
    {
        using (IMemoryOwner<T> buffer = this.MemoryAllocator.Allocate<T>(desiredLength))
        {
            T[] expectedVals = new T[buffer.Length()];

            for (int i = 0; i < buffer.Length(); i++)
            {
                Span<T> span = buffer.GetSpan();
                expectedVals[i] = getExpectedValue(i);
                span[i] = expectedVals[i];
            }

            for (int i = 0; i < buffer.Length(); i++)
            {
                Span<T> span = buffer.GetSpan();
                Assert.Equal(expectedVals[i], span[i]);
            }
        }
    }

    [Theory]
    [MemberData(nameof(LengthValues))]
    public void IndexingSpan_WhenOutOfRange_Throws_byte(int desiredLength)
    {
        this.TestIndexOutOfRangeShouldThrow<byte>(desiredLength);
    }

    [Theory]
    [MemberData(nameof(LengthValues))]
    public void IndexingSpan_WhenOutOfRange_Throws_long(int desiredLength)
    {
        this.TestIndexOutOfRangeShouldThrow<long>(desiredLength);
    }

    [Theory]
    [MemberData(nameof(LengthValues))]
    public void IndexingSpan_WhenOutOfRange_Throws_CustomStruct(int desiredLength)
    {
        this.TestIndexOutOfRangeShouldThrow<CustomStruct>(desiredLength);
    }

    private T TestIndexOutOfRangeShouldThrow<T>(int desiredLength)
        where T : struct, IEquatable<T>
    {
        T dummy = default(T);

        using (IMemoryOwner<T> buffer = this.MemoryAllocator.Allocate<T>(desiredLength))
        {
            Assert.ThrowsAny<Exception>(
                () =>
                    {
                        Span<T> span = buffer.GetSpan();
                        dummy = span[desiredLength];
                    });

            Assert.ThrowsAny<Exception>(
                () =>
                    {
                        Span<T> span = buffer.GetSpan();
                        dummy = span[desiredLength + 1];
                    });

            Assert.ThrowsAny<Exception>(
                () =>
                    {
                        Span<T> span = buffer.GetSpan();
                        dummy = span[desiredLength + 42];
                    });
        }

        return dummy;
    }

    [Fact]
    public void GetMemory_ReturnsValidMemory()
    {
        using (IMemoryOwner<CustomStruct> buffer = this.MemoryAllocator.Allocate<CustomStruct>(42))
        {
            Span<CustomStruct> span0 = buffer.GetSpan();
            span0[10].A = 30;
            Memory<CustomStruct> memory = buffer.Memory;

            Assert.Equal(42, memory.Length);
            Span<CustomStruct> span1 = memory.Span;

            Assert.Equal(42, span1.Length);
            Assert.Equal(30, span1[10].A);
        }
    }

    [Fact]
    public unsafe void GetMemory_ResultIsPinnable()
    {
        using (IMemoryOwner<int> buffer = this.MemoryAllocator.Allocate<int>(42))
        {
            Span<int> span0 = buffer.GetSpan();
            span0[10] = 30;

            Memory<int> memory = buffer.Memory;

            using (MemoryHandle h = memory.Pin())
            {
                int* ptr = (int*)h.Pointer;
                Assert.Equal(30, ptr[10]);
            }
        }
    }
}
