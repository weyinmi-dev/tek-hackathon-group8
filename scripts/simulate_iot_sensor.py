import time
import requests
import argparse
import random

API_URL = "http://localhost:5000/api/v1/towers/{}/fuel"

def send_reading(tower_code, power_source, fuel_level):
    url = API_URL.format(tower_code)
    payload = {
        "activePowerSource": power_source,
        "fuelLevelLiters": fuel_level
    }
    try:
        response = requests.post(url, json=payload)
        print(f"[IoT] Sent reading for {tower_code}: {fuel_level:.1f}L -> API Status: {response.status_code}")
    except Exception as e:
        print(f"[IoT] Connection error: {e}")

def main():
    parser = argparse.ArgumentParser(description="IoT Fuel Sensor Simulator")
    parser.add_argument("--tower", type=str, default="TWR-JOS-01", help="Tower Code to simulate")
    parser.add_argument("--start-fuel", type=float, default=500.0, help="Starting fuel in liters")
    args = parser.parse_args()

    print(f"Starting IoT Sensor Simulation for {args.tower}...")
    current_fuel = args.start_fuel

    print("Press 'Ctrl+C' to stop. Press 'Enter' in another terminal to trigger theft (not supported directly in basic loop, so we will use a probability for the demo, or wait for 5 readings).")
    
    counter = 0
    try:
        while True:
            # Normal burn rate: ~1 liter per minute
            burn = random.uniform(0.5, 1.5)
            
            # TRIGGER THEFT: Every 10th reading, simulate a huge drain of 100 Liters!
            counter += 1
            if counter == 10:
                print("🚨 SIMULATING THEFT! Draining 100 Liters instantly...")
                burn = 100.0
                counter = 0 # reset
                
            current_fuel -= burn
            
            if current_fuel < 0:
                current_fuel = 0

            send_reading(args.tower, 1, current_fuel) # 1 = Generator

            time.sleep(5)
            
    except KeyboardInterrupt:
        print("\nStopping simulation.")

if __name__ == "__main__":
    main()
