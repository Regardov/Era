using UnityEngine;

namespace Era
{
    public sealed partial class Simulation
    {
        public float PartyMultiplier => Mathf.Pow(2,Mathf.Max(0,State.players.Count-1));
        public float SpawnInterval => Mathf.Max(.32f,1.1f-Mathf.Min(10,Mathf.FloorToInt(State.stageElapsed/60))*.075f)
            /PartyMultiplier/(State.postBossActive?2:1);

        void ScaleLivingEnemies(float ratio)
        {
            // Preserve damage already dealt: current and maximum HP grow in the same ratio.
            foreach(var enemy in State.enemies){enemy.hp*=ratio;enemy.maxHp*=ratio;enemy.damage*=ratio;}
            foreach(var hazard in State.hazards)hazard.damage*=ratio;
        }
        void RefreshPartyDifficulty()
        {
            float scale=PartyMultiplier;
            if(Mathf.Approximately(scale,State.enemyPartyScale))return;
            float ratio=scale/State.enemyPartyScale;
            ScaleLivingEnemies(ratio);State.spawnTimer/=ratio;State.enemyPartyScale=scale;
        }
        void TickDifficulty(float dt)
        {
            RefreshPartyDifficulty();
            if(!State.postBossActive)return;
            State.postBossElapsed+=dt;
            int steps=Mathf.FloorToInt((State.postBossElapsed+.0001f)/10);
            while(State.postBossGrowthSteps<steps){State.postBossGrowthSteps++;ScaleLivingEnemies(1.1f);}
        }
    }
}
