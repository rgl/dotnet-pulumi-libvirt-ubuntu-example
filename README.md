# About

This creates an example libvirt QEMU/KVM Virtual Machine using dotnet [Pulumi](https://www.pulumi.com/).

**NB** For a terraform equivalent see the [rgl/terraform-libvirt-ubuntu-example](https://github.com/rgl/terraform-libvirt-ubuntu-example) repository.

## Usage (Ubuntu 20.04 host)

Create and install the [Ubuntu 20.04 vagrant box](https://github.com/rgl/ubuntu-vagrant) (because this example uses its base disk).

[Install the dotnet 6.0 SDK](https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu):

```bash
echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1' >/etc/profile.d/opt-out-dotnet-cli-telemetry.sh
source /etc/profile.d/opt-out-dotnet-cli-telemetry.sh
wget -qO packages-microsoft-prod.deb "https://packages.microsoft.com/config/ubuntu/$(lsb_release -s -r)/packages-microsoft-prod.deb"
dpkg -i packages-microsoft-prod.deb
apt-get install -y apt-transport-https
apt-get update
apt-get install -y dotnet-sdk-6.0
```

[Install Pulumi](https://www.pulumi.com/docs/get-started/install/):

```bash
wget https://get.pulumi.com/releases/sdk/pulumi-v3.39.3-linux-x64.tar.gz
sudo tar xf pulumi-v3.39.3-linux-x64.tar.gz -C /usr/local/bin --strip-components 1
rm pulumi-v3.39.3-linux-x64.tar.gz
```

Configure the stack:

```bash
cat >secrets.sh <<'EOF'
export PULUMI_SKIP_UPDATE_CHECK=true
export PULUMI_BACKEND_URL="file://$PWD" # NB pulumi will create the .pulumi sub-directory.
export PULUMI_CONFIG_PASSPHRASE='password'
EOF
```

Launch this example:

```bash
source secrets.sh
pulumi login
pulumi whoami -v
pulumi stack init dev
pulumi up
#TF_LOG=TRACE pulumi up --logtostderr --logflow -v=9 2>pulumi.log
```

Use the example:

```bash
ssh "vagrant@$(pulumi stack output IpAddress)" \
    lsblk -x KNAME -o KNAME,SIZE,TRAN,SUBSYSTEMS,FSTYPE,UUID,LABEL,MODEL,SERIAL
```

Destroy everything:

```bash
pulumi destroy
```

## Notes

* There is not yet a built-in way to execute ad-hoc provision commands à-la
  terraform `remote-exec`.
  * see https://github.com/pulumi/pulumi/issues/99
  * see https://github.com/pulumi/pulumi/issues/1691
  * see https://github.com/pulumi/examples/tree/master/aws-ts-ec2-provisioners
* This Pulumi provider is based on the [Terraform libvirt provider (dmacvicar/terraform-provider-libvirt)](https://github.com/dmacvicar/terraform-provider-libvirt).

## References

* https://www.pulumi.com/docs/intro/cloud-providers/libvirt/
* https://www.pulumi.com/docs/reference/pkg/libvirt/
* https://github.com/pulumi/pulumi-libvirt
* https://github.com/pulumi/examples/
