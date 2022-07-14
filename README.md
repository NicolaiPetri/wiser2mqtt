# UPDATE
This project is now archieved - I suggest skipping using the Wiser IP Module and use an (open) zigbee integration instead, like the Zigbee2Mqtt or similar.

# Wiser2Mqtt

**Exposes Wiser power monitor metrics to MQTT**

## Supported hardware

Currently this have been developed and tested with the Schneider Wiser IP Module (EER31800) with 2 Schneider Energy PowerTag 3PN.
[Like this](https://www.lk.dk/produkter?iid=13745)

## Configuration

### appsetttings.json 

You should probably not touch appsettings.json but either use appsettings.Development.json or appsettings.Production.json.

Section "Mqtt" is for your Mqtt connection.
Section "Wiser" is settings for your Wiser IP Module.

Example configuration :
``` json
{
  "Mqtt": {
    "Host": "10.0.0.10",
    "Port": 1883,
    "Username": "user",
    "Password": "verysecret",
    "HassAutoDiscovery": true
  },
  "Wiser": {
    "Host": "10.0.0.11",
    "Username": "m2madmin",
    "Password": "pleasefillmein",
    "UpdateInterval": 10
  }
```

### Example Home Assistant MQTT sensor configuration

``` yaml
sensor:
  - platform: mqtt
    unique_id: entrance_zone_phase1_voltage
    name: Entrance Zone Phase1 Voltage
    state_topic: wiser/Entrance/MeterInstantData
    value_template: >-
        {{ value_json.voltageAN }}
    expire_after: 120
    device_class: voltage
    state_class: measurement
    unit_of_measurement: V

  - platform: mqtt
    unique_id: entrance_zone_phase1_current
    name: Entrance Zone Phase1 Current
    state_topic: wiser/Entrance/MeterInstantData
    value_template: >-
        {{ value_json.currentA }}
    expire_after: 120
    device_class: current
    state_class: measurement
    unit_of_measurement: A

```


## Example output

### Mqtt Topic

Examples: 
 - wiser/{nameOfZone/meter}/MeterInstantData
 - wiser/{nameOfZone/meter}/MeterCumulatedData

### Payload

**MeterInstantData Payload**

``` json
{
  "currentA": 0,
  "currentB": 0,
  "currentC": 0.230000004,
  "voltageAB": 397.600006,
  "voltageBC": 398.299988,
  "voltageCA": 401,
  "voltageAN": 233.600006,
  "voltageBN": 225.600006,
  "voltageCN": 231.899994,
  "powerA": 0,
  "powerB": 0,
  "powerC": 33,
  "powerTActive": 33,
  "powerTReactive": 0,
  "powerTApparent": 0,
  "powerFactorT": 0,
  "frequency": 0,
  "slaveId": 151,
  "channel": 0
}
```


**MeterCumulatedData Payload**

``` json
{
  "energyTActive": 3059,
  "energyTReactive": 0,
  "energyTApparent": 0,
  "energyPActive": 3059,
  "energyTActiveRec": 0,
  "energyPActiveRec": 0,
  "energyPReactive": 0,
  "energyPApparent": 0,
  "slaveId": 151,
  "channel": 0
}
```

## Feedback
If you have any issue please file a bug report. If you have suggestions or just want to say thank you, then you can always send me something. Either hardware for testing or just a simple thank-you email.

