﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vinkekfish;
using keccak;   // keccak взят отсюда https://github.com/fdsc/old/releases
using BytesBuilder = vinkekfish.BytesBuilder;
using System.Runtime.CompilerServices;

namespace main_tests
{
    class KeccakSimpleHashTestByBits
    {
        TestTask task;
        public KeccakSimpleHashTestByBits(ConcurrentQueue<TestTask> tasks)
        {
            task = new TestTask("Keccak simple hash test by bits Keccak_base_20200918.getHash512 (Keccak_20200918)", StartTests);
            tasks.Enqueue(task);

            sources = SourceTask.getIterator();
        }

        class SourceTask
        {
            public string Key;
            public byte[] Value;

            public static IEnumerable<SourceTask> getIterator()
            {
                // 144 - это двойной блок хеширования keccak 512 битов
                for (long size = 1; size < 256; size++)
                {
                    for (ulong val = 0; val < (ulong) (size << 3); val++)
                    {
                        var b1 = new byte[size];
                        BytesBuilder.ToNull(b1, 0xFFFF_FFFF__FFFF_FFFF);
                        BitToBytes.resetBit(b1, val);

                        var b2 = new byte[size];
                        BytesBuilder.ToNull(b2);
                        BitToBytes.setBit(b2, val);

                        yield return new SourceTask() {Key = "byte[" + size + "] with nulls and val = " + val, Value = b1};
                        yield return new SourceTask() {Key = "byte[" + size + "] with vals = " + val, Value = b2};
                    }
                }

                yield break;
            }
        }

        IEnumerable<SourceTask> sources = null;

        public unsafe void StartTests()
        {
            foreach (var ts in sources)
            {
                var s = vinkekfish.BytesBuilder.CloneBytes(ts.Value);

                var k = new Keccak_20200918();
                byte[] h1, h2;
                fixed (byte * Sb = s)
                {
                    h1 = k.getHash512(Sb, s.LongLength);
                    h2 = new SHA3(1024).getHash512(s);
                }

                if (!vinkekfish.BytesBuilder.UnsecureCompare(s, ts.Value))
                {
                    task.error.Add(new Error() {Message = "Sources arrays has been changed for test array: " + ts.Key});
                }

                if (!vinkekfish.BytesBuilder.UnsecureCompare(h1, h2))
                {
                    task.error.Add(new Error() {Message = "Hashes are not equal for test array: " + ts.Key});
                }
            }
        }
    }
}