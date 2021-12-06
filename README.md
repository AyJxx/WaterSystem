# Water System
Created by **Adam Jůva**
- Website -> [https://adamjuva.com/](https://adamjuva.com/)
- Twitter -> [https://twitter.com/AdamJuva](https://twitter.com/AdamJuva)
- LinkedIn -> [https://www.linkedin.com/in/adamjuva/](https://www.linkedin.com/in/adamjuva/)

If you find this project helpful, you can support me by a small [donation](https://www.paypal.com/donate/?hosted_button_id=SWDA22AH63KWJ).

![Donation](https://adamjuva.com/wp-content/uploads/2020/07/Donation.png)

# Introduction

Water System gives you control over creating fully customizable water mesh which can interact with other objects.

I decided to release Water System as-is and as an open source, because of impossibility to provide more time on my side to finish and implement all features. It has few small drawbacks, which you can find below, but these are only small things and it is fully functional asset and you can definitely use it in your projects.

Demo Video: https://www.youtube.com/watch?v=kvCLOCA3V8s&ab_channel=AdamJ%C5%AFva

# Features
**Water Builder**
- Water mesh creation
- Flow map creation

**Water Base**
- Controlling water visual properties

**Water Dynamics**
- Controlling dynamics of the water (waves and foam waves)

**Water Interactions**
- Controlling interactions by other objects with the water

# Requirements
- Universal Render Pipeline
- Unity v2020+

# Guide
1. Create empty **Game Object** in the scene.
2. Add component **Water Builder** to this **Game Object** (this will automatically create water mesh with needed dependencies).
3. Adjust **Mesh Points** by creating new ones and/or editing existing.
4. Click on **Create Water Mesh** to create mesh of your shape (after that you can click on **Save Water Mesh** to save it permanently in the project, otherwise mesh is stored in the RAM).
5. Click on **Add Flow Vector** to create new flow vectors which are used for flow of the water.
6. Adjust rotation of individual **Flow Vectors** and set their properties (speed, radius, etc.).
7. Then click on **Create Flow Map** (after that you can click on **Save Flow Map** to save it permanently).
8. Adjust properties of component **Water Base** which control visual of the water.
9. Then click on **Add Water Dynamics** in component **Water Builder**.
10. Adjust properties of **Water Dynamics** to create waves, etc.
11. Then click on **Add Water Interactions** in component **Water Builder**.
12. Adjust properties of **Water Interactions** for interactions with other **Game Objects**.
13. After that you can add **Collider** to your other **Game Objects** and they will start to interact with the water.
14. You can also implement interface **IWaterEntity** in **Mono Behaviour** component on your other **Game Object** to get data of the **Water Base** this other **Game Object** is currently interacting with to perform additional calculations.
15. For rendering **Caustics**, you will need to add **Depth Normals Feature** to your **Forward Renderer**

# Known Issues
- Underwater view is not implemented.
- There is no optimization for large water mesh.
- When positioning **Mesh Points**, keep in mind that some concave shapes may not be generated properly.
- Avoid positioning of **Mesh Points** in a way that connecting lines are crossing one another.

# Credits
- [Jasper Flick](https://twitter.com/catlikecoding) -> for inspiration on some techniques
