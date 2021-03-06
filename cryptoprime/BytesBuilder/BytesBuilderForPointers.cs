﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace cryptoprime
{
    /// <summary>
    /// BytesBuilderForPointers
    /// Класс позволяет собирать большой блок байтов из более мелких
    /// Класс непотокобезопасный (при его использовании необходимо синхронизировать доступ к классу вручную)
    /// </summary>
    public unsafe partial class BytesBuilderForPointers
    {
        /// <summary>Добавленные блоки байтов</summary>
        public readonly List<Record> bytes = new List<Record>();


        /// <summary>Количество всех сохранённых байтов в этом объекте</summary>
        public long Count => count;

        /// <summary>Количество всех сохранённых блоков, как они были добавлены в этот объект</summary>
        public long countOfBlocks => bytes.Count;

        /// <summary>Получает сохранённых блок с определённым индексом в списке сохранения</summary><param name="number">Индекс в списке</param><returns>Сохранённый блок (не копия, подлинник)</returns>
        public Record getBlock(int number)
        {
            return bytes[number];
        }

        /// <summary>Количество сохранённых байтов</summary>
        protected long count = 0;

        /// <summary>Добавляет копию блока данных в объект</summary><param name="bytesToAdded">Исходный блок данных для добавления</param>
        /// <param name="len">Длина добавляемого массива</param>
        /// <param name="allocator">Аллокатор для выделения памяти для копирования</param>
        /// <param name="index">Индекс, куда добавляется блок. По-умолчанию, в конец (index = -1)</param>
        public void addWithCopy(byte * bytesToAdded, long len, AllocatorForUnsafeMemoryInterface allocator, int index = -1)
        {
            var rec = CloneBytes(bytesToAdded, 0, len, allocator);

            add(rec, index);
        }

        /// <summary>Добавляет блок данных без копирования в объект</summary><param name="bytesToAdded">Добавляемый блок данных, указатель перезаписывается нулём с целью избежания ошибочного использования. <para>Обратите внимание, что при изменении из-вне блока данных могут измениться данные и внутри объекта</para><para>При удалении блока данных в этом буфере исходные данные будут перезатёрты нулями!</para></param>
        /// <param name="len">Длина добавляемого массива</param>
        /// <param name="index">Куда добавляется блок. По-умолчанию, в конец (index = -1)</param>
        /// <remarks>Обратите внимание, массив bytesToAdded лучше после этого нигде не использовать. Так как после удаления его из буфера, он будет автоматически перезаписан нулями. Необходима доп. проверка на то, что вызывающая функция нигде не использует данный объект</remarks>
        public void addWithoutCopy(ref byte * bytesToAdded, long len, int index = -1)
        {
            var rec = new Record() { len = len, array = bytesToAdded };

            add(rec, index);

            // Перезаписываем указатель, чтобы
            bytesToAdded = null;
        }

        /// <summary>Добавляет массив в сохранённые значения без копирования. Массив будет автоматически очищен и освобождён после окончания</summary>
        /// <param name="rec">Добавляемый массив (не копируется, будет уничтожен автоматически при очистке BytesBuilder). Массив нельзя использовать где-то ещё, так как он может быть неожиданно очищен</param>
        /// <param name="index">Индекс позиции, на которую добавляется массив</param>
        public void add(Record rec, int index = -1)
        {
            if (index == -1)
                bytes.Add(rec);
            else
                bytes.Insert(index, rec);

            count += rec.len;
        }

        /// <summary>Обнуляет объект</summary>
        /// <param name="fast">fast = <see langword="false"/> - обнуляет все байты сохранённые в массиве</param>
        public void Clear(bool fast = false)
        {
            if (!fast)
            {
                foreach (Record e in bytes)
                    e.Dispose();
            }

            count = 0;
            bytes.Clear();
        }

        /// <summary>Создаёт массив байтов, включающий в себя все сохранённые массивы. Ничего не удаляет и не очищает</summary>
        /// <param name="resultCount">Размер массива-результата (если нужны все байты resultCount = -1)</param>
        /// <param name="resultA">Массив, в который будет записан результат. Если resultA = null, то массив создаётся</param>
        /// <param name="allocator">Аллокатор для выделения памяти для копирования</param>
        /// <returns>Массив байтов результата, длиной resultCount. Если установлен resultA, то возврат совпадает с этим массивом</returns>
        public Record getBytes(long resultCount = -1, Record resultA = null, AllocatorForUnsafeMemoryInterface allocator = null)
        {
            if (resultCount == -1)
                resultCount = count;

            if (resultCount > count)
            {
                throw new System.ArgumentOutOfRangeException(nameof(resultCount), "BytesBuilderForPointers.getBytes: resultCount is too large: resultCount > count");
            }

            if (resultA != null && resultA.len < resultCount)
                throw new System.ArgumentOutOfRangeException(nameof(resultCount), "BytesBuilderForPointers.getBytes: resultA is too small");

            var result = resultA ?? allocator?.AllocMemory(resultCount) ?? bytes[0].allocator.AllocMemory(resultCount);

            long cursor = 0;
            for (int i = 0; i < bytes.Count; i++)
            {
                if (cursor >= resultCount)
                    break;

                if (bytes[i].isDisposed)
                    throw new System.Exception("BytesBuilderForPointers.getBytes: bytes[i].isDisposed");

                BytesBuilder.CopyTo(bytes[i].len, result.len, bytes[i].array, result.array, cursor);
                cursor += bytes[i].len;
            }

            return result;
        }

        /// <summary>Клонирует массив, начиная с элемента start, до элемента с индексом PostEnd (не включая его)</summary><param name="b">Массив для копирования</param>
        /// <param name="start">Начальный элемент для копирования</param>
        /// <param name="PostEnd">Элемент, расположенный после последнего элемента для копирования</param>
        /// <param name="allocator">Аллокатор для выделения памяти для копирования. Не может быть null</param>
        /// <returns>Новый массив, являющийся копией массива b[start .. PostEnd - 1]</returns>
        public static unsafe Record CloneBytes(byte * b, long start, long PostEnd, AllocatorForUnsafeMemoryInterface allocator)
        {
            var result = allocator.AllocMemory(PostEnd - start);
            BytesBuilder.CopyTo(PostEnd, PostEnd - start, b, result.array, 0, -1, start);

            return result;
        }

        /// <summary>Клонирует массив, начиная с элемента start, до элемента с индексом PostEnd (не включая его)</summary>
        /// <param name="rec">Массив для копирования</param>
        /// <param name="allocator">Аллокатор для выделения памяти для копирования. Может быть null, в таком случае аллокатор получается из rec</param>
        /// <param name="start">Начальный элемент для копирования</param>
        /// <param name="PostEnd">Элемент, расположенный после последнего элемента для копирования</param>
        /// <returns>Новый массив, являющийся копией массива rec[start .. PostEnd - 1]</returns>
        public static unsafe Record CloneBytes(Record rec, AllocatorForUnsafeMemoryInterface allocator = null, long start = 0, long PostEnd = -1)
        {
            if (PostEnd < 0)
                PostEnd = rec.len;
            allocator ??= rec.allocator;

            if (allocator == null)
                throw new Exception("BytesBuilderForPointers.CloneBytes: allocator == null");

            return CloneBytes(rec.array, start: start, PostEnd: PostEnd, allocator);
        }

        /// <summary>Клонирует массив, начиная с элемента start, до элемента с индексом PostEnd (не включая его)</summary>
        /// <param name="b">Массив для копирования</param>
        /// <param name="allocator">Аллокатор для выделения памяти для копирования. Не может быть null</param>
        /// <param name="start">Начальный элемент для копирования</param>
        /// <param name="PostEnd">Элемент, расположенный после последнего элемента для копирования</param>
        /// <returns>Новый массив, являющийся копией массива b[start .. PostEnd - 1]</returns>
        public static unsafe Record CloneBytes(byte[] b, AllocatorForUnsafeMemoryInterface allocator, long start = 0, long PostEnd = -1)
        {
            if (PostEnd < 0)
                PostEnd = b.LongLength;

            fixed (byte * bb = b)
            {
                return CloneBytes(bb, start: start, PostEnd: PostEnd, allocator);
            }
        }


        /// <summary>Удаляет блок из объекта с позиции position, блок очищается нулями. <para>Эта функция служебная, скорее всего, вам не надо её вызывать</para></summary>
        /// <returns>Возвращает длину удалённого блока</returns>
        /// <param name="position">Индекс удаляемого блока</param>
        /// <param name="doClear">Если true, то удалённый блок очищается нулями и память, выделенная под него, освобождается ( всё это делается вызовом Record.Dispose() )</param>
        public long RemoveBlockAt(int position, bool doClear = true)
        {
            if (position < 0)
                throw new ArgumentException("position must be >= 0");

            if (position >= bytes.Count)
                throw new ArgumentException("position must be in range");

            var tmp = bytes[position];

            long removedLength = tmp.len;
            bytes.RemoveAt(position);

            if (doClear)
                tmp.Dispose();

            count -= removedLength;
            return removedLength;
        }

        /// <summary>Создаёт массив байтов, включающий в себя result.len символов, и удаляет их с очисткой из BytesBuilder</summary>
        /// <param name="result">Массив, в который будет записан результат. Уже должен быть выделен. result != <see langword="null"/>. Количество байтов устанавливается длиной массива. <para>Если Result.allocator, то может быть ситуация разыменования <see langword="null"/>, если нет allocator у записей, которые были добавлены в буфер</para></param>
        /// <returns>Запрошенный результат (первые result.len байтов). Этот возвращаемый результат равен параметру result</returns>
        /// <remarks>Эта функция может неожиданно обнулить часть внешнего массива, сохранённого без копирования (если он где-то используется в другом месте)</remarks>
        public Record getBytesAndRemoveIt(Record result)
        {
            long   cursor  = 0;
            Record current;
            for (int i = 0; i < bytes.Count; )
            {
                if (cursor == result.len)
                    break;

                if (cursor > result.len)
                    throw new System.Exception("Fatal algorithmic error (BytesBuilderForPointers.getBytesAndRemoveIt): cursor > resultCount");

                current = bytes[i];
                if (current.isDisposed)
                    throw new System.Exception("BytesBuilderForPointers.getBytes: current.isDisposed");

                if (cursor + current.len > result.len)
                {
                    // Делим массив на две части. Левая уходит наружу, правая остаётся в массиве
                    var left  = result.len - cursor;
                    var right = current.len - left;

                    var bLeft  = current.Clone(0, left, allocator: current.allocator ?? result.allocator);
                    var bRight = current.Clone(left,    allocator: current.allocator ?? result.allocator);
                    
                    if (bRight.len != right)
                        throw new System.Exception("Fatal algorithmic error (BytesBuilderForPointers.getBytesAndRemoveIt): bRight.len != right");

                    RemoveBlockAt(i);

                    bytes.Insert(0, bLeft );
                    bytes.Insert(1, bRight);

                    count += left + right;
                }

                // Осторожно, может быть, что bytes[i] != current
                var len   = bytes[i].len;
                var check = BytesBuilder.CopyTo(len, result.len, bytes[i].array, result.array, cursor);
                cursor += check;
                if (check != len)
                    throw new System.Exception("Fatal algorithmic error (BytesBuilderForPointers.getBytesAndRemoveIt): check != bytes[i].len");

                RemoveBlockAt(i);
            }
                                
            
            return result;
        }


        /// <summary>Получает 8-мибайтовое целое число из массива. Младший байт по младшему индексу</summary>
        /// <param name="data">Полученное число</param>
        /// <param name="target">Массив с числом</param>
        /// <param name="start">Начальный элемент, по которому расположено число</param>
        /// <param name="length">Полная длина массива, до конца должно оставаться не менее 8-ми байтов</param>
        public unsafe static void BytesToULong(out ulong data, byte * target, long start, long length)
        {
            data = 0;
            if (start < 0 || start + 8 > length)
                throw new IndexOutOfRangeException();

            for (long i = start + 8 - 1; i >= start; i--)
            {
                data <<= 8;
                data += *(target + i);
            }
        }

        /// <summary>Безопасно сравнивает два массива</summary>
        /// <param name="r1">Первый массив</param>
        /// <param name="r2">Второй массив</param>
        /// <returns><see langword="true"/>, если массивы совпадают.</returns>
        public unsafe static bool isArrayEqual_Secure(Record r1, Record r2)
        {
            return isArrayEqual_Secure(r1, r2, 0, 0, r1.len, r2.len);
        }

        /// <summary>Безопасно сравнивает два массива</summary>
        /// <param name="r1">Первый массив</param>
        /// <param name="r2">Второй массив</param>
        /// <param name="start1">Начальный индекс для сравнения в первом массиве</param>
        /// <param name="start2">Начальный индекс для сравнения во втором массиве</param>
        /// <param name="len1">Длина подмассива для сравнивания</param>
        /// <param name="len2">Длина подмассива для сравнивания</param>
        /// <returns><see langword="true"/>, если массивы совпадают.</returns>
        public unsafe static bool isArrayEqual_Secure(Record r1, Record r2, long start1, long start2, long len1, long len2)
        {
            var len = len1;
            if (len > len2)
                len = len2;

            byte * r1a = r1.array + start1, r2a = r2.array + start2, End1 = r1a + len;

            byte V = 0;
            for (; r1a < End1; r1a++, r2a++)
            {
                V |= (byte) (*r1a - *r2a);
            }

            return V == 0 && len1 == len2;
        }

        /// <summary></summary>
        ~BytesBuilderForPointers()
        {
            if (bytes.Count > 0)
            {
                Clear();
                throw new Exception("~BytesBuilderForPointers: bytes.Count > 0");
            }
        }
    }
}
