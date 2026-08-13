using SharpOSC;

// every OSC address the tool emits lives here. downstream patches listen on these
// exact strings, so don't rename them casually.
class OscBroadcaster
{
    readonly UDPSender _sender;

    public OscBroadcaster(string host, int port)
    {
        _sender = new UDPSender(host, port);
    }

    public void Fan(string label, float rpm) => Send($"/fan/{label}", rpm);
    public void Power(string component, float watts) => Send($"/power/{component}", watts);
    public void Voltage(string name, float volts) => Send($"/voltage/{name}", volts);
    public void Ram(string label, float celsius) => Send($"/ram/{label}", celsius);

    void Send(string address, float value) => _sender.Send(new OscMessage(address, value));
}
