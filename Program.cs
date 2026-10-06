// A maritime emergency coordination system that: 
// 1. Registers incidents
// 2. Tracks rescue units and their availability
// 3. Assigns suitable units to emergencies
// 4. Records a simple chronological operational log until the incident is concluded.



/* Maritime Command & Rescue Platform

Incident
 ├── ID (case number)
 ├── Type
 ├── Description
 ├── Coordinates
 ├── ReportedAt
 ├── Status
 ├── Assigned Rescue Units
 └── Operational Log Entries


Rescue Unit
 ├── Name
 ├── Type
 ├── Status
 └── Coordinates


Operational Log Entry
 ├── Incident ID
 ├── Timestamp
 ├── Coordinates
 ├── Rescue Unit
 └── Message
 */





/*REGISTER INCIDENT

Receive type, description, latitude and longitude


IF(incident type is not one of our five accepted categories) 
    Reject with "Select supported incident type"
    STOP

IF(description missing, empty or contains only spaces)
    Reject with "Please provide an incident description"
    STOP

IF(latitude is outside -90 to 90)
    Reject with "Latitude is out of range"
    STOP

IF(longitude is outside -180 to 180)
    Reject with "Longitude is out of range"
    STOP

Create incident with:
    [Id]: (Generate unique ID here)
    [ReportedAt]: (Record the current timestamp here)
    [Status]: (Reported)
    [type/Type]: (Example: Fire. Search and Rescue. Medical Emergency.)
    [Description]: (Example: "Smoke/Fire reported at Vessel.)
	[Location]: (Example: Latitude 56. Longitude 112)

Store incident
Return incident
*/