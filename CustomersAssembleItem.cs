using Kitchen;
using KitchenData;
using KitchenMods;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;

namespace KitchenMakeItYourself
{
    public class CustomersAssembleItem : GameSystemBase, IModSystem
    {
        EntityQuery GroupsAwaitingOrder;

        private class ItemDetails
        {
            public Entity Entity;
            public int ItemID;
            public bool IsGroup;
            public int ComponentCount => ComponentIDs?.Length ?? 0;
            public int[] ComponentIDs;

            public ItemDetails(Entity e, CItem cItem)
            {
                Entity = e;
                ItemID = cItem.ID;
                ComponentIDs = cItem.Items.AsArray() ?? new int[] { };
                IsGroup = cItem.IsGroup;
            }
        }

        protected override void Initialise()
        {
            base.Initialise();
            GroupsAwaitingOrder = GetEntityQuery(new QueryHelper()
                .All(
                    typeof(CGroupAwaitingOrder),
                    typeof(CWaitingForItem),
                    typeof(CGroupReward),
                    typeof(CPatience),
                    typeof(CGroupMember),
                    typeof(CCustomerSettings),
                    typeof(CAssignedTable)));
        }

        protected override void OnUpdate()
        {
            using NativeArray<Entity> entities = GroupsAwaitingOrder.ToEntityArray(Allocator.Temp);
            using NativeArray<CAssignedTable> assignedTables = GroupsAwaitingOrder.ToComponentDataArray<CAssignedTable>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                CAssignedTable assignedTable = assignedTables[i];

                if (!RequireBuffer(assignedTable, out DynamicBuffer<CTableSetGrabPoints> grabPoints))
                    continue;

                List<ItemDetails> candidateItems = new List<ItemDetails>(); 

                for (int j = 0; j < grabPoints.Length; j++)
                {
                    if (!Require(grabPoints[j], out CItemHolder holder) ||
                        !Require(holder.HeldItem, out CItem cItem))
                        continue;

                    candidateItems.Add(new ItemDetails(holder.HeldItem, cItem));
                }

                // Order from most components to least components
                candidateItems.Sort((x, y) => y.ComponentCount.CompareTo(x.ComponentCount));

                DynamicBuffer<CWaitingForItem> waitingForItems = GetBuffer<CWaitingForItem>(entity);
                for (int j = 0; j < waitingForItems.Length; j++)
                {
                    CWaitingForItem waitingForItem = waitingForItems[j];

                    if (waitingForItem.Satisfied ||
                        !Require(waitingForItem.Item, out CItem request) ||
                        !IsIngredientsForItemSatisfied(request, candidateItems, out List<ItemDetails> usedItems))
                        continue;

                    for (int k = 0; k < usedItems.Count; k++)
                    {
                        ItemDetails usedItem = usedItems[k];
                        candidateItems.RemoveAt(candidateItems.IndexOf(usedItem));

                        if (usedItem.ItemID == request.ID)
                        {
                            if (!Require(usedItem.Entity, out CItem cItem))
                            {
                                continue;
                            }
                            cItem.Items = request.Items;
                            Set(usedItem.Entity, cItem);
                            continue;
                        }
                        EntityManager.DestroyEntity(usedItem.Entity);
                    }
                }
            }
        }

        private bool IsIngredientsForItemSatisfied(CItem requestedItem, in List<ItemDetails> candidateItems, out List<ItemDetails> usedItems)
        {
            int[] requiredComponents = requestedItem.Items.AsArray();

            List<ItemDetails> baseItemCandidates = new List<ItemDetails>();
            foreach (ItemDetails candidate in candidateItems)
            {
                if (candidate.ItemID != requestedItem.ID)
                    continue;
                baseItemCandidates.Add(candidate);
            }

            usedItems = new List<ItemDetails>();
            foreach (ItemDetails baseItemCandidate in baseItemCandidates)
            {
                Dictionary<int, int> GetComponentCounts(int[] components)
                {
                    return components.Distinct().ToDictionary(x => x, x => requiredComponents.Count(y => x == y));
                }

                usedItems.Clear();
                Dictionary<int, int> requiredComponentCounts = GetComponentCounts(requiredComponents);

                bool baseIsSatisfied = true;
                foreach (int componentID in baseItemCandidate.ComponentIDs)
                {
                    if (!requiredComponentCounts.TryGetValue(componentID, out int requiredCount) ||
                        requiredCount == 0)
                    {
                        baseIsSatisfied = false;
                        break;
                    }
                    requiredComponentCounts[componentID]--;
                }
                if (!baseIsSatisfied)
                    continue;

                usedItems.Add(baseItemCandidate);

                bool HasRemainingRequiredComponents()
                {
                    return requiredComponentCounts.Where(x => x.Value > 0).Any();
                }

                bool isComplete = false;

                foreach (ItemDetails candidate in candidateItems)
                {
                    if (candidate.ItemID == requestedItem.ID)
                        continue;

                    Dictionary<int, int> candidateComponentCounts;
                    if (candidate.IsGroup)
                        candidateComponentCounts = GetComponentCounts(candidate.ComponentIDs);
                    else
                    {
                        candidateComponentCounts = new Dictionary<int, int>()
                        {
                            { candidate.ItemID, 1 }
                        };
                    }
                    bool satisfiesRequiredComponents = !candidateComponentCounts.Where(x => !requiredComponentCounts.TryGetValue(x.Key, out int requiredCount) || requiredCount < x.Value).Any();
                    if (!satisfiesRequiredComponents)
                        continue;

                    foreach (KeyValuePair<int, int> kvp in candidateComponentCounts)
                    {
                        requiredComponentCounts[kvp.Key] -= kvp.Value;
                    }

                    usedItems.Add(candidate);

                    isComplete = !HasRemainingRequiredComponents();

                    if (isComplete)
                        break;
                }

                if (!isComplete)
                    continue;

                return true;
            }
            usedItems.Clear();
            return false;
        }
    }
}
