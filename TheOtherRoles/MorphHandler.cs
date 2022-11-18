using System.Diagnostics;
namespace TheOtherRoles
{
    public static class MorphHandler
    {
        public static void morphToPlayer(this PlayerControl pc, PlayerControl target)
        {
            setOutfit(pc, target.Data.DefaultOutfit, target.Visible);
        }

        public static void setOutfit(this PlayerControl pc, GameData.PlayerOutfit outfit, bool visible = true)
        {
            StackFrame stack1 = new(1);
            StackFrame stack2 = new(2);
            Logger.info($"{pc?.getNameWithRole()} => {outfit?.PlayerName} at {stack1.GetMethod().Name} at {stack2.GetMethod().Name}", "setOutfit");
            pc.Data.Outfits[PlayerOutfitType.Shapeshifted] = outfit;
            pc.CurrentOutfitType = PlayerOutfitType.Shapeshifted;

            pc.RawSetName(outfit.PlayerName);
            pc.RawSetHat(outfit.HatId, outfit.ColorId);
            pc.RawSetVisor(outfit.VisorId, outfit.ColorId);
            pc.RawSetColor(outfit.ColorId);
            Helpers.setSkinWithAnim(pc.MyPhysics, outfit.SkinId);

            if (pc.cosmetics.currentPet) UnityEngine.Object.Destroy(pc.cosmetics.currentPet.gameObject);
            if (!pc.Data.IsDead)
            {
                pc.cosmetics.currentPet = UnityEngine.Object.Instantiate<PetBehaviour>(FastDestroyableSingleton<HatManager>.Instance.GetPetById(outfit.PetId).viewData.viewData);
                pc.cosmetics.currentPet.transform.position = pc.transform.position;
                pc.cosmetics.currentPet.Source = pc;
                pc.cosmetics.currentPet.Visible = visible;
                pc.SetPlayerMaterialColors(pc.cosmetics.currentPet.rend);
            }
        }

        public static void resetMorph(this PlayerControl pc)
        {
            morphToPlayer(pc, pc);
            Munou.reMorph(pc.PlayerId);
            pc.CurrentOutfitType = PlayerOutfitType.Default;
        }
    }

}
