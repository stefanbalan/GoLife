﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace GoLife
{


    public abstract class GameOfLifeBase<TWorld> : IGame<TWorld> where TWorld : class, IWorld, new()
    {
        public const bool Alive = true;
        public const bool Dead = false;
        private readonly ConcurrentQueue<Generation<TWorld>> _iterations;
        protected TWorld currentGeneration;
        protected Generation<TWorld> cg;
        protected TWorld nextGeneration;
        protected Generation<TWorld> ng;

        public bool HighlightChanges { get; set; }
        public bool StartFromPrevious { get; set; }


        protected GameOfLifeBase()
        {
            _iterations = new ConcurrentQueue<Generation<TWorld>>();
            cg = new Generation<TWorld> { Live = new TWorld() };
            _iterations.Enqueue(cg);

            TargetTimeMs = 1000;
        }

        public void Initialize(Generation<TWorld> generation)
        {
            cg = generation;
            currentGeneration = cg.Live;
            _iterations.Enqueue(cg);
        }

        public bool GetCellAt(int x, int y)
        {
            return cg.Live[x, y];
        }

        public void SetCellAt(int x, int y, bool alive = true)
        {
            cg.Live[x, y] = alive;
        }

        #region Game
        //public abstract bool IsAlive(int i, int cellPositionX);

        public abstract int GetNumberOfAliveNeighbours(int i, int cellPositionX);

        public abstract void ComputeNextGeneration();
        #endregion


        public int TargetTimeMs { get; set; }
        public int AverageTimeMs { get; private set; }
        public TWorld CurrentGeneration => (TWorld)currentGeneration?.Clone();

        public bool Stop { get; set; }

        public void Run()
        {
            var sw = new Stopwatch();
            do
            {
                sw.Start();
                currentGeneration = cg?.Live == null ? new TWorld() : (TWorld)((CellWorld)cg.Live.Clone())
                    .Add(cg.Born)
                    .Remove(cg.Dead);
                ng = new Generation<TWorld>
                {
                    Live = StartFromPrevious ? (TWorld)currentGeneration.Clone() : new TWorld()
                };
                nextGeneration = ng.Live;

                try
                {
                    ComputeNextGeneration();
                }
                catch (Exception ex)
                {

                }
                _iterations.Enqueue(ng);
                cg = ng;
                sw.Stop();

                var ms = sw.ElapsedMilliseconds;
                Debug.WriteLine($"ComputeNextGeneration: {ms}ms");
                if (ms < TargetTimeMs)
                    Thread.Sleep((int)(TargetTimeMs - ms));
                if (_iterations.Count > 8)
                {
                    Thread.Sleep((int)(7 * ms));
                }
                sw.Reset();
                AverageTimeMs = (int)((AverageTimeMs * 7 + ms) / 8);

            } while (!Stop);
        }


        public Generation<TWorld> TryGetNext()
        {
            return _iterations.TryDequeue(out var result)
                ? result
                : null;
        }

    }
}
