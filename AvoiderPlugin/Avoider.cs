using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AvoiderPlugin
{
    /// <summary>
    /// Add to a NavMesh Agent. Keeps eye contact with the "avoidee" and, when the avoidee
    /// is in range and can see this object, runs to the closest nearby spot the avoidee
    /// cannot see (found with Poisson-disc sampling).
    /// </summary>
    [AddComponentMenu("Avoider/Avoider")]
    [DisallowMultipleComponent]
    public class Avoider : MonoBehaviour
    {
        [Header("Required")]
        [Tooltip("The object to avoid (e.g. the player). Drag it here.")]
        public Transform avoidee;

        [Header("Behaviour")]
        [Tooltip("Avoidee must be this close before the avoider reacts.")]
        [Min(0.1f)] public float range = 10f;

        [Tooltip("Run speed (applied to the NavMesh Agent).")]
        [Min(0.1f)] public float speed = 5f;

        [Tooltip("How often (seconds) the avoider re-thinks.")]
        [Min(0.05f)] public float thinkInterval = 0.25f;

        [Tooltip("How fast the avoider turns to keep eye contact.")]
        [Min(0.1f)] public float turnSpeed = 8f;

        [Header("Hiding spot search")]
        [Tooltip("Hiding spots are searched within this radius around the avoider.")]
        [Min(1f)] public float searchRadius = 12f;

        [Tooltip("Minimum distance between the Poisson-disc sample points.")]
        [Min(0.5f)] public float pointSpacing = 2f;

        [Tooltip("Height of the 'eyes' used for line-of-sight checks.")]
        public float eyeHeight = 1f;

        [Tooltip("Layers that block line of sight (walls, obstacles...).")]
        public LayerMask obstacleMask = ~0;

        [Header("Debug")]
        [Tooltip("Draw gizmos for this avoider.")]
        public bool showGizmos = true;

        private NavMeshAgent agent;
        private NavMeshPath path;
        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

        // Stored only for gizmo drawing.
        private readonly List<Vector3> visiblePoints = new List<Vector3>();
        private readonly List<Vector3> hiddenPoints = new List<Vector3>();
        private Vector3 chosenSpot;
        private bool hasChosenSpot;
        private bool avoideeCanSeeMe;

        // Runs when the component is first added in the editor.
        private void Reset()
        {
            CheckSetup(true);
        }

        private void Start()
        {
            if (!CheckSetup(true))
            {
                enabled = false;
                return;
            }

            agent.speed = speed;
            agent.updateRotation = false; // we handle rotation to keep eye contact
            path = new NavMeshPath();
            StartCoroutine(ThinkLoop());
        }

        /// <summary>Returns true if everything needed is set up. Warns the developer otherwise.</summary>
        private bool CheckSetup(bool logWarnings)
        {
            agent = GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                if (logWarnings)
                    Debug.LogWarning("[Avoider] '" + name + "' is not a NavMesh Agent. " +
                        "Add a NavMeshAgent component to it and bake a NavMesh for the scene.", this);
                return false;
            }

            if (Application.isPlaying)
            {
                if (!agent.isOnNavMesh)
                {
                    if (logWarnings)
                        Debug.LogWarning("[Avoider] '" + name + "' is not on a NavMesh. " +
                            "Bake a NavMesh for the scene (Window > AI > Navigation) and place the agent on it.", this);
                    return false;
                }

                if (avoidee == null)
                {
                    if (logWarnings)
                        Debug.LogWarning("[Avoider] '" + name + "' has no avoidee. " +
                            "Drag the object to avoid (e.g. the player) into the Avoidee field.", this);
                    return false;
                }
            }
            return true;
        }

        private void Update()
        {
            if (agent == null || avoidee == null) return;

            agent.speed = speed; // lets you tweak speed live in the inspector

            // Maintain eye contact (turn around the Y axis only).
            Vector3 toAvoidee = avoidee.position - transform.position;
            toAvoidee.y = 0f;
            if (toAvoidee.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(toAvoidee);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);
            }
        }

        /* GENERAL LOOP */
        private IEnumerator ThinkLoop()
        {
            var wait = new WaitForSeconds(thinkInterval);

            while (true)
            {
                if (avoidee != null && agent.isOnNavMesh)
                {
                    bool inRange = (avoidee.position - transform.position).sqrMagnitude <= range * range;
                    avoideeCanSeeMe = inRange && IsPointVisibleToAvoidee(transform.position);

                    if (avoideeCanSeeMe)
                    {
                        Vector3 spot;
                        if (TryFindHidingSpot(out spot))
                            agent.SetDestination(spot);
                    }
                }
                yield return wait; // wait a bit and check again
            }
        }

        /* FIND A SPOT */
        private bool TryFindHidingSpot(out Vector3 bestSpot)
        {
            bestSpot = transform.position;
            bool found = false;
            float bestDistSqr = float.MaxValue;

            visiblePoints.Clear();
            hiddenPoints.Clear();

            float size = searchRadius * 2f;
            var sampler = new PoissonDiscSampler(size, size, pointSpacing);

            foreach (Vector2 p in sampler.Samples())
            {
                // Sampler points are in [0,size]; center them on the avoider (XZ plane).
                Vector3 world = new Vector3(
                    transform.position.x + p.x - searchRadius,
                    transform.position.y,
                    transform.position.z + p.y - searchRadius);

                // Snap onto the NavMesh; ignore points that are off it.
                NavMeshHit navHit;
                if (!NavMesh.SamplePosition(world, out navHit, 2f, agent.areaMask))
                    continue;
                Vector3 point = navHit.position;

                if (IsPointVisibleToAvoidee(point))
                {
                    if (showGizmos) visiblePoints.Add(point);
                    continue; // avoidee can see it: ignore
                }

                if (showGizmos) hiddenPoints.Add(point);

                // Candidate. Keep it only if it is the closest so far AND reachable.
                float distSqr = (point - transform.position).sqrMagnitude;
                if (distSqr < bestDistSqr &&
                    agent.CalculatePath(point, path) &&
                    path.status == NavMeshPathStatus.PathComplete)
                {
                    bestDistSqr = distSqr;
                    bestSpot = point;
                    found = true;
                }
            }

            hasChosenSpot = found;
            if (found) chosenSpot = bestSpot;
            return found;
        }

        /* CHECK VISIBILITY TO POINT */
        // True if nothing (other than the avoidee/avoider themselves) blocks the line
        // from the avoidee's eyes to the given point.
        private bool IsPointVisibleToAvoidee(Vector3 point)
        {
            Vector3 from = avoidee.position + Vector3.up * eyeHeight;
            Vector3 to = point + Vector3.up * eyeHeight;
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist < 0.01f) return true;

            int count = Physics.RaycastNonAlloc(from, dir / dist, hitBuffer, dist,
                obstacleMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Transform t = hitBuffer[i].transform;
                if (t.IsChildOf(avoidee) || t.IsChildOf(transform))
                    continue; // hitting the avoidee or myself doesn't count as blocked
                return false; // something is in the way: point is hidden
            }
            return true;
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            // Range
            Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, range);

            // Eye contact line (red when the avoidee can see me)
            if (avoidee != null)
            {
                Gizmos.color = avoideeCanSeeMe ? Color.red : Color.green;
                Gizmos.DrawLine(transform.position + Vector3.up * eyeHeight,
                                avoidee.position + Vector3.up * eyeHeight);
            }

            // Sample points: red = seen by avoidee, blue = hidden candidates
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            foreach (Vector3 p in visiblePoints)
            {
                Gizmos.DrawLine(transform.position, p);
                Gizmos.DrawSphere(p, 0.15f);
            }

            Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.8f);
            foreach (Vector3 p in hiddenPoints)
            {
                Gizmos.DrawLine(transform.position, p);
                Gizmos.DrawSphere(p, 0.15f);
            }

            // Chosen escape spot
            if (hasChosenSpot)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, chosenSpot);
                Gizmos.DrawWireSphere(chosenSpot, 0.4f);
            }
        }
    }
}