﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace GoL
{
    public struct Position
    {
        public int X;
        public int Y;
    }

    /// <summary>
    /// A sparse array of booleans (stored as bit)
    /// </summary>
    public class SparseArrayWorld : IWorld
    {
        internal int _version;
        private readonly SortedList<int, SortedList<int, ulong>> _rows;

        public SparseArrayWorld()
        {
            _rows = new SortedList<int, SortedList<int, ulong>>();
        }

        public SparseArrayWorld(SparseArrayWorld existing)
        {
            _rows = new SortedList<int, SortedList<int, ulong>>(existing._rows.Count);
            foreach (var row in existing._rows)
            {
                var existingRow = row.Value;
                var newRow = new SortedList<int, ulong>(existingRow.Count);
                _rows[row.Key] = newRow;
                foreach (var key in existingRow.Keys)
                {
                    newRow[key] = existingRow[key];
                }
            }
        }

        public bool this[int x, int y]
        {
            get
            {
                var rowIndex = _rows.IndexOfKey(y);
                if (rowIndex < 0) return false;
                var row = _rows.Values[rowIndex];

                var colIndex = row.IndexOfKey(x >> 6);
                if (colIndex < 0) return false;
                var memCell = row.Values[colIndex];

                return ((memCell >> (x & 63)) & 1) == 1;
            }
            set
            {
                var rowIndex = _rows.IndexOfKey(y);
                SortedList<int, ulong> row;
                if (rowIndex < 0)
                {
                    row = new SortedList<int, ulong>();
                    _rows.Add(y, row);
                }
                else row = _rows.Values[rowIndex];

                var memIndex = x >> 6;
                var colIndex = row.IndexOfKey(memIndex);
                ulong memCell = 0;
                if (colIndex < 0)
                {
                    if (!value) return; // row is empty, setting to false is redundant, not changing version
                    row.Add(memIndex, memCell);
                }
                else
                {
                    memCell = row.Values[colIndex];
                }

                if (value)
                {
                    memCell |= (ulong)1 << (x & 0b111111);
                }
                else
                {
                    memCell &= ~((ulong)1 << (x & 0b111111));
                }
                row[memIndex] = memCell;
                _version += 1;

                if (memCell != 0) return;
                row.RemoveAt(colIndex);
                if (row.Count == 0)
                {
                    _rows.RemoveAt(rowIndex);
                }
            }
        }

        public IEnumerator<Position> GetEnumerator()
        {
            return new SparseArrayEnumerator(this);
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public class SparseArrayEnumerator : IEnumerator<Position>
        {
            private readonly SparseArrayWorld _world;
            private readonly SortedList<int, SortedList<int, ulong>> _rows;
            private SortedList<int, ulong> row = new SortedList<int, ulong>();

            private Position _current;
            private readonly int _version;

            private int rowIndex = -1, colIndex = -1, memIndex = -1;
            private ulong memCell;

            internal SparseArrayEnumerator(SparseArrayWorld world)
            {
                _world = world;
                _rows = world._rows;
                if (_rows.Count > 0)
                {
                    row = _rows.Values[0];
                    rowIndex = 0;
                }
                _version = world._version;
            }

            public bool MoveNext()
            {
                if (_version != _world._version)
                    throw new InvalidOperationException("Collection was modified");
                if (!FindNext()) return false;
                _current.X = (row.Keys[colIndex] << 6) + memIndex;
                _current.Y = _rows.Keys[rowIndex];
                return true;
            }

            private bool FindNext()
            {
                if (memCell != 0 && FindNextBit())
                {
                    return true;
                }

                //i'm done with the current memCell, get next memCell
                if (FindNextMemCell())
                    return true;

                // i'm done with the last memCell of the row, get new row
                do
                {
                    if (rowIndex + 1 >= _rows.Count)
                    {
                        row = new SortedList<int, ulong>(); //leave an empty row in place
                        return false; //i'm done with the last row
                    }
                    rowIndex += 1;
                } while (rowIndex < _rows.Count && _rows.Values[rowIndex].Count == 0);
                colIndex = -1;
                row = _rows.Values[rowIndex];
                return FindNextMemCell();

                bool FindNextBit()
                {
                    do
                    {
                        var firstBitIsClear = (memCell & 1) == 0;
                        memIndex += 1;
                        memCell >>= 1;
                        if (firstBitIsClear) continue;
                        return true;
                    } while (memIndex < 63);
                    return false;
                }

                bool FindNextMemCell()
                {
                    if (colIndex + 1 >= row.Count) return false;
                    colIndex += 1;
                    memCell = row.Values[colIndex]; //it should not be 0
                    memIndex = -1;
                    return FindNextBit();
                }
            }

            public void Reset()
            {
                rowIndex = -1; colIndex = -1; memIndex = -1; memCell = 0;
            }

            public Position Current => _current;
            object IEnumerator.Current => _current;

            public void Dispose()
            {
                row = null;
            }

        }

        public object Clone()
        {
            return new SparseArrayWorld(this);
        }
    }
}