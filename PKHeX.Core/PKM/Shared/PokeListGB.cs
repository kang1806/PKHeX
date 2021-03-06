﻿using System;
using System.Linq;

namespace PKHeX.Core
{
    public abstract class PokeListGB<T> where T : PKM
    {
        private readonly int StringLength;
        private readonly byte[] Data;
        private readonly byte Capacity;
        private readonly int Entry_Size;

        public readonly T[] Pokemon;

        public byte Count { get => Data[0]; private set => Data[0] = value > Capacity ? Capacity : value; }

        protected PokeListGB(byte[] d, PokeListType c = PokeListType.Single, bool jp = false)
        {
            Data = d ?? GetEmptyList(c, jp);
            StringLength = GetStringLength(jp);
            Capacity = (byte)c;
            Entry_Size = GetEntrySize();
            var dataSize = 2 + (Capacity * (Entry_Size + 1 + (2 * StringLength)));

            if (Data.Length != dataSize)
                Array.Resize(ref Data, dataSize);

            Pokemon = Read();
        }

        protected PokeListGB(PokeListType c = PokeListType.Single, bool jp = false)
            : this(null, c, jp) => Count = 1;

        protected PokeListGB(T pk)
            : this(PokeListType.Single, pk.Japanese)
        {
            this[0] = pk;
            Count = 1;
        }

        private byte[] GetEmptyList(PokeListType c, bool jp = false)
        {
            int capacity = (byte)c;
            var intro = Enumerable.Repeat((byte) 0xFF, capacity + 1);
            var pkm = Enumerable.Repeat((byte) 0, GetEntrySize() * capacity);
            var strings = Enumerable.Repeat((byte) 0x50, GetStringLength(jp) * 2 * capacity);
            return new[] { (byte)0 }.Concat(intro).Concat(pkm).Concat(strings).ToArray();
        }

        private int GetOffsetPKMData(int base_ofs, int i) => base_ofs + (Entry_Size * i);
        private int GetOffsetPKMOT(int base_ofs, int i) => GetOffsetPKMData(base_ofs, Capacity) + (StringLength * i);
        private int GetOffsetPKMNickname(int base_ofs, int i) => GetOffsetPKMOT(base_ofs, Capacity) + (StringLength * i);

        private static int GetStringLength(bool jp) => jp ? PK1.STRLEN_J : PK1.STRLEN_U;
        protected bool IsFormatParty => IsCapacityPartyFormat((PokeListType)Capacity);
        protected static bool IsCapacityPartyFormat(PokeListType Capacity) => Capacity == PokeListType.Single || Capacity == PokeListType.Party;

        protected static int GetDataSize(PokeListType c, bool jp, int entrySize)
        {
            var entryLength = 1 + entrySize + (2 * GetStringLength(jp));
            return 2 + ((byte)c * entryLength);
        }

        protected abstract int GetEntrySize();
        protected abstract byte GetSpeciesBoxIdentifier(T pk);
        protected abstract T GetEntry(byte[] dat, byte[] otname, byte[] nick, bool egg);

        public T this[int i]
        {
            get
            {
                if (i > Capacity || i < 0) throw new ArgumentOutOfRangeException($"Invalid {nameof(PokeListGB<T>)} Access: {i}");
                return Pokemon[i];
            }
            set
            {
                if (value == null) return;
                Pokemon[i] = (T)value.Clone();
            }
        }

        private T[] Read()
        {
            var arr = new T[Capacity];
            int base_ofs = 2 + Capacity;
            for (int i = 0; i < Capacity; i++)
                arr[i] = GetEntry(base_ofs, i);
            return arr;
        }

        public byte[] Write()
        {
            int count = Array.FindIndex(Pokemon, pk => pk.Species == 0);
            Count = count < 0 ? Capacity : (byte)count;
            int base_ofs = 2 + Capacity;
            for (int i = 0; i < Count; i++)
            {
                Data[1 + i] = GetSpeciesBoxIdentifier(Pokemon[i]);
                SetEntry(base_ofs, i);
            }
            Data[1 + Count] = byte.MaxValue;
            return Data;
        }

        private T GetEntry(int base_ofs, int i)
        {
            byte[] dat = new byte[Entry_Size];
            byte[] otname = new byte[StringLength];
            byte[] nick = new byte[StringLength];
            int pkOfs = GetOffsetPKMData(base_ofs, i);
            int otOfs = GetOffsetPKMOT(base_ofs, i);
            int nkOfs = GetOffsetPKMNickname(base_ofs, i);
            Buffer.BlockCopy(Data, pkOfs, dat, 0, Entry_Size);
            Buffer.BlockCopy(Data, otOfs, otname, 0, StringLength);
            Buffer.BlockCopy(Data, nkOfs, nick, 0, StringLength);
            return GetEntry(dat, otname, nick, Data[1 + i] == 0xFD);
        }

        private void SetEntry(int base_ofs, int i)
        {
            int pkOfs = GetOffsetPKMData(base_ofs, i);
            int otOfs = GetOffsetPKMOT(base_ofs, i);
            int nkOfs = GetOffsetPKMNickname(base_ofs, i);
            Array.Copy(Pokemon[i].Data, 0, Data, pkOfs, Entry_Size);
            Array.Copy(Pokemon[i].OT_Trash, 0, Data, otOfs, StringLength);
            Array.Copy(Pokemon[i].Nickname_Trash, 0, Data, nkOfs, StringLength);
        }
    }
}