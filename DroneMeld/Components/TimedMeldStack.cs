using RoR2;
using UnityEngine;

namespace MinionMeld.Components
{
    [RequireComponent(typeof(CharacterMaster))]
    public class TimedMeldStack : MonoBehaviour
    {
        private float stopwatch;
        private bool started;

        public void Activate(float time)
        {
            if (!this.started)
            {
                this.stopwatch = time;
                this.started = true;
            }
        }

        private void FixedUpdate()
        {
            if (!this.started)
                return;

            this.stopwatch -= Time.fixedDeltaTime;
            if (this.stopwatch <= 0f)
            {
                var cm = this.GetComponent<CharacterMaster>();
                if (cm && cm.inventory)
                    cm.inventory.RemoveItem(MinionMeldPlugin.meldStackItem);

                Destroy(this);
            }
        }
    }

}
