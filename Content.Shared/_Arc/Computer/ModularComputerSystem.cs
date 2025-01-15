using Content.Shared.Containers.ItemSlots;
using Content.Shared.Coordinates;
using Robust.Shared.Audio;
using Content.Shared.Audio;
using Robust.Shared.Network;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio.Systems;
using Content.Shared.Popups;
using Content.Shared.Examine;
using Content.Shared.Interaction;

namespace Content.Shared._Arc.Computer;
public sealed class ModularComputerSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;

    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    [Dependency] private readonly INetManager _netMan = default!;

    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModularComputerComponent, EntInsertedIntoContainerMessage>(InsertDisk);
        SubscribeLocalEvent<ModularComputerComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<ModularComputerComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
    }

    private void OnExamined(EntityUid uid, ModularComputerComponent component, ExaminedEvent args)
    {
        if (!TryComp(uid, out ItemSlotsComponent? slots))
            return;

        if (!_itemSlots.TryGetSlot(uid, component.DiskSlot, out var diskSlot, slots))
            return;

        if (diskSlot.Item == null || !TryComp(diskSlot.Item, out ComputerDiskComponent? diskComp))
        {
            args.PushMarkup($"This computer doesn't have a program loaded.");
            return;
        }

        if (diskComp.ProgramPrototypeEntity == null)
        {
            args.PushMarkup($"This computer doesn't have a program loaded. An error on the display reports that the loaded disk has no program.");
            return;
        }

        args.PushMarkup($"This computer has the {EntityManager.GetComponent<MetaDataComponent>(diskComp.ProgramPrototypeEntity.Value).EntityName} program loaded.");
    }
    private void OnActivate(EntityUid uid, ModularComputerComponent component, ActivateInWorldEvent args)
    {
        // go figure it out yourself
        if (!TryComp(uid, out ItemSlotsComponent? slots))
            return;

        if (!_itemSlots.TryGetSlot(uid, component.DiskSlot, out var diskSlot, slots))
            return;

        if (diskSlot.Item == null || !TryComp(diskSlot.Item, out ComputerDiskComponent? diskComp))
        {
            if (_netMan.IsServer)
                _popupSystem.PopupEntity("ERROR: No program loaded!", uid, args.User);
            return;
        }

        if (_netMan.IsServer)
        {
            if (diskComp.ProgramPrototypeEntity == null)
            {
                _popupSystem.PopupEntity("ERROR: No program on disk!", uid, args.User);
                return;
            }

            var activateMsg = new ActivateInWorldEvent(args.User, diskComp.ProgramPrototypeEntity.Value, true);
            RaiseLocalEvent(diskComp.ProgramPrototypeEntity.Value, activateMsg);
        }
    }

    private void InsertDisk(EntityUid uid, ModularComputerComponent component, EntInsertedIntoContainerMessage args)
    {

        if (args.Container.ID != component.DiskSlot)
            return;

        UpdateComputer(new Entity<ModularComputerComponent>(uid, component));

        if (!TryComp(uid, out ItemSlotsComponent? slots))
            return;

        if (!_itemSlots.TryGetSlot(uid, component.DiskSlot, out var diskSlot, slots))
            return;

        if (diskSlot.Item == null || !TryComp(diskSlot.Item, out ComputerDiskComponent? diskComp))
            return;

        if (diskComp.ProgramPrototypeEntity != null)
        {
            if (_netMan.IsServer)
                _audioSystem.PlayPvs(component.DiskInsertSound, uid, AudioParams.Default.WithVolume(+4f));
        }
    }


    private void UpdateComputer(Entity<ModularComputerComponent> computer)
    {
        if (!TryComp(computer.Owner, out ItemSlotsComponent? slots))
            return;

        if (!_itemSlots.TryGetSlot(computer.Owner, computer.Comp.DiskSlot, out var diskSlot, slots))
            return;

        if (diskSlot.Item == null || !TryComp(diskSlot.Item, out ComputerDiskComponent? diskComp))
            return;

        if (diskComp.ProgramPrototype == "UnburnedDiskProtototype")
            return;

        EntityUid magicComputerEntity;

        if (diskComp.ProgramPrototypeEntity == null || diskComp.PersistState != true)
        {
            if (diskComp.ProgramPrototypeEntity != null)
                QueueDel(diskComp.ProgramPrototypeEntity.Value);

            magicComputerEntity = Spawn(diskComp.ProgramPrototype, computer.Owner.ToCoordinates());
            diskComp.ProgramPrototypeEntity = magicComputerEntity;
        }
        else
        {
            magicComputerEntity = diskComp.ProgramPrototypeEntity.Value;
        }

        _transform.SetParent(magicComputerEntity, diskSlot.Item.Value);

    }
}
