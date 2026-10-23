// Wheelchair interactive device - Arduino controller
// Upload this fixed sketch once. After that, Unity can reset the state with GAME_RESET.

// Encoder 1
const int PinA1 = 2;
const int PinB1 = 3;

// Encoder 2
const int PinA2 = 4;
const int PinB2 = 5;

long count1 = 0;
long count2 = 0;
int lastStateA1;
int lastStateA2;

// Buttons
const int btnRandomInput = 10;
const int btnConfirmInput = 9;
const int btnAttackInput = 8;

// LEDs
const int ledRandomOutput = 12;
const int ledConfirmOutput = 11;
const int ledAttackOutput = 7;

bool isConfirmLightOn = true;

void setup() {
  pinMode(PinA1, INPUT_PULLUP);
  pinMode(PinB1, INPUT_PULLUP);
  pinMode(PinA2, INPUT_PULLUP);
  pinMode(PinB2, INPUT_PULLUP);

  pinMode(btnRandomInput, INPUT);
  pinMode(btnConfirmInput, INPUT);
  pinMode(btnAttackInput, INPUT);

  pinMode(ledRandomOutput, OUTPUT);
  pinMode(ledConfirmOutput, OUTPUT);
  pinMode(ledAttackOutput, OUTPUT);

  Serial.begin(9600);
  Serial.setTimeout(5);

  lastStateA1 = digitalRead(PinA1);
  lastStateA2 = digitalRead(PinA2);

  resetGameState();
}

void loop() {
  checkUnityCommand();
  readEncoders();
  readButtons();
}

void readEncoders() {
  bool changed = false;

  int currentStateA1 = digitalRead(PinA1);
  if (currentStateA1 != lastStateA1) {
    if (digitalRead(PinB1) != currentStateA1) {
      count1++;
    } else {
      count1--;
    }
    changed = true;
    lastStateA1 = currentStateA1;
  }

  int currentStateA2 = digitalRead(PinA2);
  if (currentStateA2 != lastStateA2) {
    if (digitalRead(PinB2) != currentStateA2) {
      count2++;
    } else {
      count2--;
    }
    changed = true;
    lastStateA2 = currentStateA2;
  }

  if (changed && (count1 % 2 == 0 || count2 % 2 == 0)) {
    Serial.print("MOVE:");
    Serial.print(count1);
    Serial.print(",");
    Serial.println(count2);
  }
}

void readButtons() {
  int randomBtnState = digitalRead(btnRandomInput);
  int confirmBtnState = digitalRead(btnConfirmInput);
  int attackBtnState = digitalRead(btnAttackInput);

  if (randomBtnState == HIGH) {
    if (digitalRead(ledRandomOutput) == HIGH) {
      Serial.println("SPAWN_RANDOM");
      delay(200);
    }
  }

  digitalWrite(ledConfirmOutput, isConfirmLightOn ? HIGH : LOW);

  if (confirmBtnState == HIGH && isConfirmLightOn) {
    Serial.println("CONFIRM_PLACEMENT");
    isConfirmLightOn = false;
    digitalWrite(ledConfirmOutput, LOW);
    delay(200);
  }

  if (attackBtnState == HIGH) {
    Serial.println("ATTACK_ON");
    digitalWrite(ledAttackOutput, HIGH);
  } else {
    digitalWrite(ledAttackOutput, LOW);
  }
}

void checkUnityCommand() {
  while (Serial.available() > 0) {
    String command = Serial.readStringUntil('\n');
    command.trim();

    if (command == "LIGHT_RANDOM_ON") {
      digitalWrite(ledRandomOutput, HIGH);
    } else if (command == "LIGHT_RANDOM_OFF") {
      digitalWrite(ledRandomOutput, LOW);
    } else if (command == "GAME_RESET") {
      resetGameState();
    } else if (command == "B_ON") {
      // Keep this command available for Unity slope feedback.
    } else if (command == "B_OFF") {
      // Keep this command available for Unity slope feedback.
    }
  }
}

void resetGameState() {
  isConfirmLightOn = true;
  count1 = 0;
  count2 = 0;
  lastStateA1 = digitalRead(PinA1);
  lastStateA2 = digitalRead(PinA2);

  digitalWrite(ledRandomOutput, LOW);
  digitalWrite(ledConfirmOutput, HIGH);
  digitalWrite(ledAttackOutput, LOW);
}
