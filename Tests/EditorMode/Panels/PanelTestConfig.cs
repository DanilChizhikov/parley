using System;
using UnityEngine;

namespace DTech.Parley.Tests.EditorMode
{
    internal sealed class PanelTestConfig : ScriptableObject
    {
        [SerializeField] private int _health = 100;
        [SerializeField] private float _speed = 2.5f;
        [SerializeField] private Stats _stats = new ();

        public int Health => _health;
        public float Speed => _speed;
        public int Armor => _stats.Armor;

        [Serializable]
        private sealed class Stats
        {
            [SerializeField] private int _armor = 5;

            public int Armor => _armor;
        }
    }
}
