using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Pulumi;
using Pulumi.Libvirt;
using Pulumi.Libvirt.Inputs;
using Pulumi.Command.Remote;
using Pulumi.Command.Remote.Inputs;

return await Deployment.RunAsync(() =>
{
    var sshUser = "vagrant";
    var sshPrivateKeyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh/id_rsa");
    var sshPrivateKey = File.ReadAllText(sshPrivateKeyPath).Trim();
    var sshPublicKeyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh/id_rsa.pub");
    var sshPublicKeyJson = JsonSerializer.Serialize(File.ReadAllText(sshPublicKeyPath).Trim());

    // create a cloud-init cloud-config.
    // NB this creates an iso image that will be used by the NoCloud cloud-init datasource.
    // see https://www.pulumi.com/docs/reference/pkg/libvirt/cloudinitdisk/
    // see journalctl -u cloud-init
    // see /run/cloud-init/*.log
    // see https://cloudinit.readthedocs.io/en/latest/topics/examples.html#disk-setup
    // see https://cloudinit.readthedocs.io/en/latest/topics/datasources/nocloud.html#datasource-nocloud
    var cloudInit = new CloudInitDisk("cloud-init", new CloudInitDiskArgs
    {
        UserData = $@"
#cloud-config
fqdn: example.test
manage_etc_hosts: true
users:
  - name: {sshUser}
    passwd: '$6$rounds=4096$NQ.EmIrGxn$rTvGsI3WIsix9TjWaDfKrt9tm3aa7SX7pzB.PSjbwtLbsplk1HsVzIrZbXwQNce6wmeJXhCq9YFJHDx9bXFHH.'
    lock_passwd: false
    ssh-authorized-keys:
      - {sshPublicKeyJson}
disk_setup:
  /dev/sdb:
    table_type: mbr
    layout:
      - [100, 83]
    overwrite: false
fs_setup:
  - label: data
    device: /dev/sdb1
    filesystem: ext4
    overwrite: false
mounts:
  - [/dev/sdb1, /data, ext4, 'defaults,discard,nofail', '0', '2']
runcmd:
  - sed -i '/vagrant insecure public key/d' /home/vagrant/.ssh/authorized_keys
",
    });

    var bootVolume = new Volume("boot", new VolumeArgs
    {
        BaseVolumeName = "ubuntu-22.04-amd64_vagrant_box_image_0.0.0_box.img",
        Format = "qcow2",
        // NB its not yet possible to create larger disks.
        //    see https://github.com/pulumi/pulumi-libvirt/issues/6
        //Size = 66*1024*1024*1024, // 66GiB. the root FS is automatically resized by cloud-init growpart (see https://cloudinit.readthedocs.io/en/latest/topics/examples.html#grow-partitions).
    });

    var dataVolume = new Volume("data", new VolumeArgs
    {
        Format = "qcow2",
        // NB its not yet possible to create larger disks.
        //    see https://github.com/pulumi/pulumi-libvirt/issues/6
        Size = 1*1024*1024*1024,
    });

    var network = new Network("example", new NetworkArgs
    {
        Mode = "nat",
        Domain = "example.test",
        Addresses = new[] {"10.17.3.0/24"},
        Dhcp = new NetworkDhcpArgs
        {
            Enabled = false,
        },
        Dns = new NetworkDnsArgs
        {
            Enabled = true,
            LocalOnly = false,
        },
    });

    var domain = new Domain("example", new DomainArgs
    {
        Cpu = new DomainCpuArgs
        {
            Mode = "host-passthrough",
        },
        Vcpu = 2,
        Memory = 1024,
        QemuAgent = true,
        NetworkInterfaces = new DomainNetworkInterfaceArgs
        {
            NetworkId = network.Id,
            WaitForLease = true,
            Addresses = new[] {"10.17.3.2"},
        },
        Cloudinit = cloudInit.Id,
        Disks = new[]
        {
            new DomainDiskArgs
            {
                VolumeId = bootVolume.Id,
                Scsi = true,
            },
            new DomainDiskArgs
            {
                VolumeId = dataVolume.Id,
                Scsi = true,
            },
        },
        Description = $"path: {Environment.CurrentDirectory}\nproject: {Pulumi.Deployment.Instance.ProjectName}\nstack: {Pulumi.Deployment.Instance.StackName}\n",
    });

    var informationCommand = new Command("information", new CommandArgs
    {
        Connection = new ConnectionArgs
        {
            Host = domain.NetworkInterfaces.GetAt(0).Apply(n => n.Addresses[0]),
            User = sshUser,
            PrivateKey = sshPrivateKey,
        },
        Create = "/bin/bash",
        Stdin = @"
set -euxo pipefail

# redirect stderr to stdout.
exec 2>&1

# wait for cloud-init to finish.
cloud-init status --long --wait

# show information about the block devices.
lsblk -x KNAME -o KNAME,SIZE,TRAN,SUBSYSTEMS,FSTYPE,UUID,LABEL,MODEL,SERIAL
",
    });

    return new Dictionary<string, object?>
    {
        ["IpAddress"] = domain.NetworkInterfaces.GetAt(0).Apply(n => n.Addresses[0]),
        ["Information"] = Output.CreateSecret(informationCommand.Stdout),
    };
});
