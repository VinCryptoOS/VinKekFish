﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cryptoprime;
using cryptoprime.VinKekFish;

using static cryptoprime.BytesBuilderForPointers;
using static cryptoprime.VinKekFish.VinKekFishBase_etalonK1;

namespace vinkekfish
{
    public unsafe partial class VinKekFishBase_KN_20210525: IDisposable
    {
        protected bool isHaveOutputData = false;
        protected bool isInit1 = false;
        protected bool isInit2 = false;

                                                                /// <summary>Если <see langword="true"/>, то выполнена инициализация 1 (сгенерированы таблицы перестановок)</summary>
        public bool IsInit1 => isInit1;                         /// <summary>Если <see langword="true"/>, то выполнена инициализация 2 (полная инициализация состояния)</summary>
        public bool IsInit2 => isInit2;


        /// <summary>Осуществляет непосредственный шаг алгоритма без ввода данных и изменения tweak</summary><remarks>Вызывайте эту функцию, если хотите переопределить поведение VinKekFish</remarks>
        /// <param name="countOfRounds">Количество раундов. См. </param>
        public void step(int countOfRounds = -1)
        {
            if (countOfRounds > CountOfRounds)
                throw new ArgumentOutOfRangeException("VinKekFishBase_KN_20210525.step: CountOfRounds > this.CountOfRounds");

            if (!isInit1)
                throw new Exception("VinKekFishBase_KN_20210525.step: you must call Init1 (and Init2 too?) before doing this");

            if (countOfRounds < 0)
                countOfRounds = this.CountOfRounds;

            var TB = tablesForPermutations;
            State1Main = true;

            // Предварительное преобразование
            doPermutation(transpose128_3200);
            doThreeFish();
            doPermutation(transpose128_3200);

            BytesBuilder.CopyTo(CryptoStateLen, CryptoStateLen, State2, State1); State1Main = true;

            // Основной шаг алгоритма: раунды
            // Каждая итерация цикла - это полураунд
            countOfRounds <<= 1;
            for (int round = 0; round < countOfRounds; round++)
            {
                doKeccak();
                doPermutation(TB); TB += CryptoStateLen;

                doThreeFish();
                doPermutation(TB); TB += CryptoStateLen;

                // Довычисление tweakVal для второго преобразования VinKekFish
                // Вычисляем tweak для данного раунда (работаем со старшим 4-хбайтным словом младшего 8-мибайтного слова tweak)
                // Каждый раунд берёт +2 к старшему 4-хбайтовому слову; +1 - после первого полураунда, и +1 - после второго полураунда
                Tweaks[2+0] += 0x1_0000_0000U;  // Берём элемент [1], расположение tweak см. по метке :an6c5JhGzyOO
            }

            // После последнего раунда производится заключительное преобразование (заключительная рандомизация) поблочной функцией keccak-f
            for (int i = 0; i < CountOfFinal; i++)
            {
                doKeccak();
                doPermutation(transpose200_3200);
                doKeccak();
                doPermutation(transpose200_3200_8);
            }

            if (!State1Main)
                throw new Exception("VinKekFishBase_KN_20210525.step: Fatal algorithmic error: !State1Main");

            isHaveOutputData = true;
        }

        /// <summary>Предварительная инициализация объекта. Осуществляет установку таблиц перестановок.</summary>
        /// <param name="PreRoundsForTranspose">Количество раундов, которое будет происходить со стандартными таблицами (не зависящими от ключа)</param>
        /// <param name="keyForPermutations">Дополнительный ключ: ключ для определения таблиц перестановок</param>
        /// <param name="key_length">Длина ключа</param>
        /// <param name="OpenInitVectorForPermutations">Дополнительный вектор инициализации</param>
        /// <param name="OpenInitVectorForPermutations_length">Длина дополнительного вектора инициализации</param>
        public virtual void Init1(int PreRoundsForTranspose = 8, Record keyForPermutations = null, Record OpenInitVectorForPermutations = null)
        {
            if (!State1Main)
                throw new Exception("VinKekFishBase_KN_20210525.Init1: Fatal algorithmic error: !State1Main");

            lock (this)
            {
                Clear();
                tablesForPermutations = VinKekFish_k1_base_20210419.GenStandardPermutationTables(CountOfRounds, allocator, key: keyForPermutations, key_length: keyForPermutations, OpenInitVector: OpenInitVectorForPermutations, OpenInitVector_length: OpenInitVectorForPermutations);
                isInit1    = true;
            }
        }

        /// <summary>Вторая инициализация (полная инициализация): инициализация внутреннего состояния ключём</summary>
        /// <param name="key"></param>
        /// <param name="OpenInitializationVector"></param>
        /// <param name="TweakInit"></param>
        /// <param name="RoundsForFinal"></param>
        /// <param name="RoundsForFirstKeyBlock"></param>
        /// <param name="RoundsForTailsBlock"></param>
        /// <param name="NoFinalOverwrite">Если true, то заключительный шаг впитывания ключа происходит без перезаписывания нулями входного блока (нет дополнительной необратимости)</param>
        public virtual void Init2(Record key = null, Record OpenInitializationVector = null, Record TweakInit = null, int RoundsForFinal = NORMAL_ROUNDS, int RoundsForFirstKeyBlock = NORMAL_ROUNDS, int RoundsForTailsBlock = MIN_ROUNDS, bool FinalOverwrite = true)
        {
            if (!State1Main)
                throw new Exception("VinKekFishBase_KN_20210525.Init2: Fatal algorithmic error: !State1Main");

            lock (this)
            {
                ClearState();
                StartThreads();

                InputKey(key: key, OpenInitializationVector: OpenInitializationVector, TweakInit: TweakInit, RoundsForFinal: RoundsForFinal, RoundsForFirstKeyBlock: RoundsForFirstKeyBlock, RoundsForTailsBlock: RoundsForTailsBlock, FinalOverwrite: FinalOverwrite);
                isInit2 = true;
            }
        }

        protected virtual void StartThreads()
        {
            foreach (var t in threads)
            {
                if (t.ThreadState != ThreadState.Running)
                    t.Start();
            }
        }

        /// <summary>Простая инициализация объекта без ключа для принятия энтропии в дальнейшем</summary>
        public virtual void SimpleInit()
        {
            Init1();
            Init2(RoundsForFirstKeyBlock: 0, RoundsForFinal: 0, FinalOverwrite: false);
        }

        protected virtual void InputKey(Record key = null, Record OpenInitializationVector = null, Record TweakInit = null, int RoundsForFinal = NORMAL_ROUNDS, int RoundsForFirstKeyBlock = NORMAL_ROUNDS, int RoundsForTailsBlock = MIN_ROUNDS, bool FinalOverwrite = true)
        {
            if (!State1Main)
                throw new Exception("VinKekFishBase_KN_20210525.InputKey: Fatal algorithmic error: !State1Main");

            if (TweakInit != null && TweakInit.len >= CryptoTweakLen * 2)
            {
                var T     = (ulong *) TweakInit;
                Tweaks[0] = T[0];
                Tweaks[1] = T[1];
            }

            if (OpenInitializationVector != null)
            {
                if (OpenInitializationVector > MAX_OIV_K)
                    throw new ArgumentOutOfRangeException("VinKekFishBase_KN_20210525.InputKey: OpenInitializationVector > MAX_OIV");

                byte len1 = (byte) OpenInitializationVector;
                byte len2 = (byte) (OpenInitializationVector >> 8);

                BytesBuilder.CopyTo(OpenInitializationVector, MAX_OIV_K, OpenInitializationVector, State1 + MAX_SINGLE_KEY_K + 2);
                State1[MAX_SINGLE_KEY_K + 2 + 0] ^= len1;
                State1[MAX_SINGLE_KEY_K + 2 + 1] ^= len2;
            }

            long keyLen = key;
            var dt      = keyLen;
            if (key != null)
            {
                if (dt > MAX_SINGLE_KEY_K)
                    dt = MAX_SINGLE_KEY_K;

                byte len1 = (byte) dt;
                byte len2 = (byte) (dt >> 8);
                State1[0] ^= len1;
                State1[1] ^= len2;

                BytesBuilder.CopyTo(dt, MAX_SINGLE_KEY_K, key, State1 + 2);
                keyLen -= dt;
            }

            step(RoundsForFirstKeyBlock);

            var TailOfKey = key.array + dt;         // key + dt - это будет неверно! , см. перегрузку оператора "+" в Record
            while (keyLen > 0)
            {
                dt = keyLen;
                if (dt > BLOCK_SIZE_K)
                {
                    dt = BLOCK_SIZE_K;
                }

                byte len1 = (byte) dt;
                byte len2 = (byte) (dt >> 8);
                State1[0] ^= len1;
                State1[1] ^= len2;

                XorWithBytes(dt, TailOfKey, State1 + 2);

                keyLen    -= dt;
                TailOfKey += dt;

                step(RoundsForTailsBlock);
            }

            // После инициализации обнуляем часть данных для обеспечения необратимости
            if (FinalOverwrite)
                BytesBuilder.ToNull(BLOCK_SIZE_K, State1 + 2);

            step(RoundsForFinal);
        }

        /// <summary>Производит наложение на массив t массива s с помощью операции xor</summary>
        /// <param name="len">Длина массива s</param>
        /// <param name="s">Налагаемый массив (не изменяется)</param>
        /// <param name="t">Изменяемый массив</param>
        public static void XorWithBytes(long len, byte * s, byte * t)
        {
            var len8 = len >> 3;
            var s8   = (ulong*) s;
            var t8   = (ulong*) t;

            while (len8 > 0)
            {
                *t8 ^= *s8;
                t8++; s8++; len8--;
            }

            len -= len8 << 3;
            s = (byte *) s8;
            t = (byte *) t8;
            while (len > 0)
            {
                *t ^= *s;
                t++; s++; len--;
            }
        }
// TODO: Сделать ввод через xor параллельным
        public void InputData_Overwrite(byte * data, long dataLen, byte regime, bool nullPadding = true)
        {
            if (dataLen > BLOCK_SIZE_K)
                throw new ArgumentOutOfRangeException();
            if (!State1Main)
                throw new Exception("VinKekFishBase_KN_20210525.InputData_Overwrite: Fatal algorithmic error: !State1Main");

            if (nullPadding)
            {
                var paddingLen = BLOCK_SIZE_K - dataLen;
                if (paddingLen > 0)
                    BytesBuilder.ToNull(paddingLen, State1 + 3 + dataLen);
            }

            BytesBuilder.CopyTo(dataLen, Len, data, State1 + 3);

            byte len1 = (byte) dataLen;
            byte len2 = (byte) (dataLen >> 8);

            len2 |= 0x80;       // Старший бит количества вводимых байтов устанавливается в 1, если используется режим Overwrite
            if (!nullPadding)   // Второй (начиная с 1) по старшинству бит устанавливаем, если не перезатирали значения нулями
            {
                if ((len2 & 0x40) > 0)
                    throw new Exception("InputData_Overwrite: fatal algorithmic error: (len2 & 0x40) > 0");

                len2 |= 0x40;
            }

            State1[0] ^= len1;
            State1[1] ^= len2;
            State1[2] ^= regime;

            InputData_ChangeTweak(dataLen: dataLen, Overwrite: true, regime: regime);
        }

        public void InputData_Xor(byte * data, long dataLen, byte regime)
        {
            if (dataLen > BLOCK_SIZE)
                throw new ArgumentOutOfRangeException();

            XorWithBytes(dataLen, data, State1 + 3);

            byte len1 = (byte) dataLen;
            byte len2 = (byte) (dataLen >> 8);

            State1[0] ^= len1;
            State1[1] ^= len2;
            State1[2] ^= regime;

            InputData_ChangeTweak(dataLen: dataLen, Overwrite: false, regime: regime);
        }

        /// <summary>Изменяет tweak. Этот метод вызывать не надо. Он автоматически вызывается при вызове InputData_*</summary>
        public void InputData_ChangeTweak(long dataLen, bool Overwrite, byte regime)
        {
            // Приращение tweak перед вводом данных
            Tweaks[0] += TWEAK_STEP_NUMBER;

            Tweaks[1] += (ulong) dataLen;
            if (Overwrite)
                Tweaks[1] += 0x0100_0000_0000_0000;

            var reg = ((ulong) regime) << 40; // 8*5 - третий по старшинству байт, нумерация с 1
            Tweaks[1] += reg;
            State1[2] ^= regime;
        }

        /// <summary>Если никаких данных не введено в режиме Sponge (xor), изменяет tweak</summary>
        public void NoInputData_ChangeTweak(byte regime)
        {
            // Приращение tweak перед вводом данных
            Tweaks[0] += TWEAK_STEP_NUMBER;

            var reg = ((ulong) regime) << 40; // 8*5 - третий по старшинству байт, нумерация с 1
            Tweaks[1] += reg;
            State1[2] ^= regime;
        }
    }
}
