Module twin — desired properties (paste in Azure Portal)

1. IoT Hub → Devices → your Edge device → Device details → Module identities → DeviceDataManagerModule → Module Identity Twin.
2. Under "properties" → "desired", paste the JSON from module-twin-desired.example.json (replace placeholders).
3. The same JSON shape is embedded in deployment.template.json under modulesContent.DeviceDataManagerModule.properties.desired for deployment-based configuration.

Your SQL must return two columns named "label" (text) and "value" (bigint). Use @maxRows in countQuery when you want the twin maxRows cap applied.

Direct method: invoke module method "GetCounts" on DeviceDataManagerModule (payload can be empty JSON {}).
