﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;



using System.IO;

using System.Reflection;

using KSP;
using KSP.IO;
using UnityEngine;

using KSPPluginFramework;


namespace scatterer
{
	[KSPAddon(KSPAddon.Startup.EveryScene, false)]
	public class Core : MonoBehaviourWindow
	{
		PluginConfiguration cfg = KSP.IO.PluginConfiguration.CreateForType<SkyNode>(null);

		[Persistent] List<String> scattererPlanets= new List<String> {};// { "Kerbin", "Duna", "Eeloo" };

		MeshRenderer mr = new MeshRenderer ();

		bool initiated=false;

		float alphaCutoff=100f;



		int fadeStart=55000;
		int fadeEnd=60000;

		int farplane=60000;
		int nearplane=60000;

		float rimBlend=100f;
		float rimpower=600f;

		Camera[] cams;
		int count;




		public float[] additionalScales=new float[10];

		//bool[] debugSettings= new bool[5];
		
		//postprocessing properties
		//float inscatteringCoeff=85f; //useless, removed from shader too
		float extinctionCoeff=70f;
		float postProcessingalpha=78f;
		float postProcessDepth=200f;
		float postProcessScale=1000f;
		float postProcessExposure=18f;

		public int renderQueue=2000;
		int renderQueue2=2010;


		float apparentDistance=1000f;

		public Camera chosenCamera;

		//sky properties
		float exposure = 20f;
		float alphaGlobal=100f;
		public int layer=15;
		int cam=2;
		
		//other stuff
		float atmosphereGlobalScale=1000f;
		float m_radius;// = 600000.0f;
		String parentPlanet="Kerbin";
		int PlanetId;
		int SunId;				
		CelestialBody[] celestialBodies;	
		Manager m_manager;
		bool depthbufferEnabled=false;
		bool isActive;
		
		
		public Transform GetScaledTransform(string body)
		{
			List<Transform> transforms = ScaledSpace.Instance.scaledSpaceTransforms;
			return transforms.Single(n => n.name == body);
		}
		
		internal override void Awake()
		{


			WindowCaption = "Scatterer mod: alt+f10/f11 toggle";
			WindowRect = new Rect(0, 0, 300, 50);
			Visible = true;						
			isActive = false;
			

			if (HighLogic.LoadedScene == GameScenes.TRACKSTATION)
			{
				ReactivateAtmosphere (parentPlanet, rimBlend, rimpower);
			}

//			savePlanetsList ();


			if (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedScene==GameScenes.SPACECENTER )

			{
				isActive = true;




				for (int j=0; j<10; j++)
				{
					additionalScales[j]=1000f;
				}

//				for (int j=0;j<5;j++){
//					debugSettings[j]=false;
//				}
				
				//read parent planet from config
				cfg.load ();
				parentPlanet =cfg.GetValue<string>("Planet");
				atmosphereGlobalScale=float.Parse(cfg.GetValue<string>("atmosphereGlobalScale"))*1000f;
				
				//find sun and parent planet
				celestialBodies = (CelestialBody[])CelestialBody.FindObjectsOfType(typeof(CelestialBody));
				PlanetId =0;
				SunId =0;

				Transform transform; // this next bit finds kerbin and the sun, and sets all the plaents to renderqueue 2002
									 //so they don't get clipped over by the amospheres (renderqueue 2001)

				for (int k=0; k< celestialBodies.Length ; k++)
				{
						transform = GetScaledTransform (celestialBodies[k].name);													
						{
							mr = (MeshRenderer)transform.GetComponent (typeof(MeshRenderer));
							if (mr != null)
							{															
								mr.material.renderQueue=2002;
							}
						}										

					if (celestialBodies[k].GetName() == parentPlanet)
						PlanetId=k;
					
					if (celestialBodies[k].GetName() == "Sun")
						SunId=k;
//					mr.enabled=false;
				}
				
				m_manager=new Manager();
				m_manager.setParentCelestialBody(celestialBodies[PlanetId]);
				m_manager.setSunCelestialBody(celestialBodies[SunId]);
				m_manager.SetCore(this);
				m_manager.Awake();
				getSettingsFromSkynode();


				
				m_radius = (float)celestialBodies [PlanetId].Radius;

//				MeshRenderer sunMR=
//					(MeshRenderer)celestialBodies[SunId].GetComponent (typeof(MeshRenderer));
//				sunMR.enabled=false;

			}					
		}
		
		
		
		internal override void Update()
		{			
			//toggle whether its visible or not
			if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && (Input.GetKeyDown(KeyCode.F11)||(Input.GetKeyDown(KeyCode.F10))))
				Visible = !Visible;
			if (isActive)
			{
				m_manager.Update ();

				if ((!MapView.MapIsEnabled)&&(!m_manager.m_skyNode.inScaledSpace))
				{
					DeactivateAtmosphere(parentPlanet);
					//print ("STOCK ATMO DISABLED");
				}
				
				else
				{
					rimBlend=100f;   //kerbin settings, not sure about other planets
					rimpower=600f;
					ReactivateAtmosphere(parentPlanet,rimBlend,rimpower);
					//print ("STOCK ATMO ENABLED");
					
				}
			}

			loadPlanetsList();
			print (scattererPlanets.Count);
//
			print (scattererPlanets [0]);
			print (scattererPlanets [1]);
			print (scattererPlanets [2]);
//
//			print ("QualitySettings.activeColorSpace");
//			print (QualitySettings.activeColorSpace);
		}
			
		void OnGUI()
		{
			//debugging for rendertextures, not needed anymore but might be when I implement oceans
			//	GUI.DrawTexture(new Rect(250,250,512,512), m_transmit, ScaleMode.StretchToFill, false);
			//	GUI.DrawTexture(new Rect(250,250,512,512), RenderTexture.active, ScaleMode.StretchToFill, false);
		}

		internal override void OnDestroy()
		{
//			base.OnDestroy ();
//			ReactivateAtmosphere(parentPlanet,rimBlend,rimpower);
//			m_manager.m_skyNode.OnDestroy ();
//			Destroy(m_manager.m_skyNode);
			m_manager.OnDestroy ();
			Destroy (m_manager);

		}
		
		internal override void DrawWindow(int id)
		{
			
			
			DragEnabled = true;
			
			//			GUILayout.BeginHorizontal();
			//			GUILayout.Label(String.Format("Drag Enabled:{0}",DragEnabled.ToString()));
			//			if (GUILayout.Button("Toggle Drag"))
			//            DragEnabled = !DragEnabled;
			//			GUILayout.EndHorizontal();
			
			
			GUILayout.Label (String.Format ("In game:{0}", isActive.ToString ()));
			GUILayout.BeginHorizontal ();
			if (GUILayout.Button ("Hide"))
				Visible = !Visible;
			GUILayout.EndHorizontal ();

			if (!initiated) {
				cams = Camera.allCameras;
				count = Camera.allCameras.Length;
				initiated=true;
			}
			
			
			if (isActive)
			{
				//setting up lots of properties here, not the most elegant way to do it
				//but since the GUI is just for testing It'll remain here for now
				
				
				//CAM DEBUG OPTIONS								
//								GUILayout.Label(String.Format("Number of cams:{0}",count.ToString()));
//								GUILayout.Label (String.Format ("cam1pos:{0}", cams [0].transform.position.ToString ()));
//								GUILayout.Label (String.Format ("cam1Name:{0}", cams [0].name));
//								GUILayout.Label (String.Format ("cam2pos:{0}", cams [1].transform.position.ToString ()));
//								GUILayout.Label (String.Format ("cam2pos:{0}", cams [1].name));
//								GUILayout.Label (String.Format ("cam3pos:{0}", cams [2].transform.position.ToString ()));
//								GUILayout.Label (String.Format ("cam3pos:{0}", cams [2].name));
//								GUILayout.Label (String.Format ("cam4pos:{0}", cams [3].transform.position.ToString ()));
//								GUILayout.Label (String.Format ("cam4pos:{0}", cams [3].name));
//								GUILayout.Label (String.Format ("cam5pos:{0}", cams [4].transform.position.ToString ()));
//								GUILayout.Label (String.Format ("cam5pos:{0}", cams [4].name));
//								GUILayout.Label (String.Format ("cam6pos:{0}", cams [5].transform.position.ToString ()));
//								GUILayout.Label (String.Format ("cam6pos:{0}", cams [5].name));
//				
//								if (Camera.allCameras.Length == 7) {
//									GUILayout.Label (String.Format ("cam7pos:{0}", cams [6].transform.position.ToString ()));
//					GUILayout.Label (String.Format ("cam7pos:{0}", cams [6].name));}
																							
				//END CAM DEBUG OPTIONS
				

				
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Layer");
				layer = Convert.ToInt32 (GUILayout.TextField (layer.ToString ()));
				if (GUILayout.Button ("+"))
					layer = layer + 1;
				
				if (GUILayout.Button ("-"))
					layer = layer - 1;
				
				GUILayout.EndHorizontal ();
				
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Cam");
				cam = Convert.ToInt32 (GUILayout.TextField (cam.ToString ()));
				
				if (GUILayout.Button ("+"))
					cam = cam + 1;
				
				if (GUILayout.Button ("-"))
					cam = cam - 1;
				
				GUILayout.EndHorizontal ();
				
				
				
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Alpha Global (/100)");
				alphaGlobal = (float)(Convert.ToDouble (GUILayout.TextField (alphaGlobal.ToString ())));
				
				if (GUILayout.Button ("Set"))
				{
					m_manager.m_skyNode.SetAlphaGlobal (alphaGlobal / 100);
				}
				GUILayout.EndHorizontal ();

				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Alpha Cutoff (/10000)");
				alphaCutoff = (float)(Convert.ToDouble (GUILayout.TextField (alphaCutoff.ToString ())));
				
				if (GUILayout.Button ("Set"))
				{
					m_manager.m_skyNode.SetAlphaCutoff (alphaCutoff / 10000);
				}
				GUILayout.EndHorizontal ();
				
				
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Exposure (/100)");
				exposure = (float)(Convert.ToDouble (GUILayout.TextField (exposure.ToString ())));
				
				if (GUILayout.Button ("Set"))
				{
					m_manager.m_skyNode.SetExposure (exposure / 100);
				}
				GUILayout.EndHorizontal ();
				
				
				
				m_manager.m_skyNode.setLayernCam (layer, cam);
				
				GUILayout.BeginHorizontal ();
				
				if (GUILayout.Button ("Toggle depth buffer"))
				{
					if (!depthbufferEnabled)
					{
						cams [cam].gameObject.AddComponent (typeof(ViewDepthBuffer));
						depthbufferEnabled = true;
					}
					else
					{
						Component.Destroy (cams [cam].gameObject.GetComponent<ViewDepthBuffer> ());
						depthbufferEnabled = false;
					}
				}
				GUILayout.EndHorizontal ();
				
				
				GUILayout.BeginHorizontal ();
				
				if (GUILayout.Button ("Toggle PostProcessing"))
				{

					if (!m_manager.m_skyNode.postprocessingEnabled)
					{
						m_manager.m_skyNode.enablePostprocess ();
					}
					else
					{
						m_manager.m_skyNode.disablePostprocess ();
					}
				}
				GUILayout.EndHorizontal ();
				
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Post Processing Alpha (/100)");
				postProcessingalpha = (float)(Convert.ToDouble (GUILayout.TextField (postProcessingalpha.ToString ())));
				
				if (GUILayout.Button ("Set"))
				{
					m_manager.m_skyNode.SetPostProcessAlpha (postProcessingalpha / 100);
				}
				GUILayout.EndHorizontal ();
				
				
				
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Post Processing Depth (/10000)");
				postProcessDepth = (float)(Convert.ToDouble (GUILayout.TextField (postProcessDepth.ToString ())));
				
				if (GUILayout.Button ("Set"))
				{
					m_manager.m_skyNode.SetPostProcessDepth (postProcessDepth / 10000);
				}
				GUILayout.EndHorizontal ();
				
				
				
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Post Processing Exposure (/100)");
				postProcessExposure = (float)(Convert.ToDouble (GUILayout.TextField (postProcessExposure.ToString ())));
				
				if (GUILayout.Button ("Set"))
				{
					m_manager.m_skyNode.SetPostProcessExposure (postProcessExposure / 100);
				}
				GUILayout.EndHorizontal ();
				
				
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Post Processing Scale (/1000)");
				postProcessScale = (float)(Convert.ToDouble (GUILayout.TextField (postProcessScale.ToString ())));
				
				if (GUILayout.Button ("Set"))
				{
					m_manager.m_skyNode.SetPostProcessScale (postProcessScale / 1000);
				}
				GUILayout.EndHorizontal ();
				
				
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Planet radius (display only)");
				GUILayout.TextField (celestialBodies [PlanetId].Radius.ToString ());
				GUILayout.EndHorizontal ();
				
				
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("atmosphereGlobalScale (/1000)");
				
				
				atmosphereGlobalScale = (float)(Convert.ToDouble (GUILayout.TextField (atmosphereGlobalScale.ToString ())));
				
				if (GUILayout.Button ("Set"))
				{
					m_manager.m_skyNode.SetAtmosphereGlobalScale (atmosphereGlobalScale / 1000);
				}
				GUILayout.EndHorizontal ();
				
				
				
				//				GUILayout.BeginHorizontal ();
				//				GUILayout.Label ("Inscattering Coeff (/100)");
				//				inscatteringCoeff = (float)(Convert.ToDouble (GUILayout.TextField (inscatteringCoeff.ToString ())));
				//				
				//				if (GUILayout.Button ("Set"))
				//				{
				//					m_manager.m_skyNode.SetInscatteringCoeff (inscatteringCoeff / 100);
				//				}
				//				GUILayout.EndHorizontal ();
				
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("Extinction Coeff (/10000)");
				
				
				extinctionCoeff = (float)(Convert.ToDouble (GUILayout.TextField (extinctionCoeff.ToString ())));
				
				if (GUILayout.Button ("Set"))
				{
					m_manager.m_skyNode.SetExtinctionCoeff (extinctionCoeff / 10000);
				}
				GUILayout.EndHorizontal ();


//				for (int j=0;j<7;j++){
//				GUILayout.BeginHorizontal ();
//				GUILayout.Label (String.Format("Debug setting:{0}", j.ToString()));	
//				GUILayout.TextField(m_manager.m_skyNode.debugSettings[j].ToString());
//				
//				if (GUILayout.Button ("Toggle"))
//				{
//						m_manager.m_skyNode.debugSettings[j] = !m_manager.m_skyNode.debugSettings[j];
//				}
//				GUILayout.EndHorizontal ();
//				}

//				for (int j=0;j<10;j++){
//
//				GUILayout.BeginHorizontal ();
//
//				GUILayout.Label (String.Format("AdditionalScale:{0}", j.ToString()));	
//
//				additionalScales[j]= (float) Convert.ToDouble(GUILayout.TextField(additionalScales[j].ToString()));
//				
//				if (GUILayout.Button ("Set"))
//				{
//					m_manager.m_skyNode.additionalScales[j] = additionalScales[j]/1000f;
//				}
//				GUILayout.EndHorizontal ();
//				}

//				GUILayout.BeginHorizontal ();
//				GUILayout.Label ("Apparent distance (/10000)");
//				apparentDistance = (float)(Convert.ToDouble (GUILayout.TextField (apparentDistance.ToString ())));
//				
//				if (GUILayout.Button ("Set"))
//				{
//					m_manager.m_skyNode.apparentDistance= apparentDistance/1000f;
//				}
//				GUILayout.EndHorizontal ();

//				GUILayout.BeginHorizontal ();
//				GUILayout.Label ("RenderQueue");
//				renderQueue = Convert.ToInt32 (GUILayout.TextField (renderQueue.ToString ()));
//				
//				if (GUILayout.Button ("Set"))
//				{
//					m_manager.m_skyNode.renderQueue= renderQueue;
//				}
//				GUILayout.EndHorizontal ();

//				GUILayout.BeginHorizontal ();
//
//				if (GUILayout.Button ("Destroy tester"))
//				{
//					m_manager.m_skyNode.destroyTester();
//				}
//				GUILayout.EndHorizontal ();

//				GUILayout.BeginHorizontal ();
//				
//				if (GUILayout.Button ("Toggle stock sunglare"))
//				{
//					m_manager.m_skyNode.toggleStockSunglare();
//				}
//				GUILayout.EndHorizontal ();

				GUILayout.BeginHorizontal ();
				
				if (GUILayout.Button ("Save settings"))
				{
					m_manager.m_skyNode.saveToConfigNode();
				}

				if (GUILayout.Button ("Load settings"))
				{
					m_manager.m_skyNode.loadFromConfigNode();
					getSettingsFromSkynode();
				}
				GUILayout.EndHorizontal ();

				GUILayout.BeginHorizontal ();

				GUILayout.Label (String.Format("ForceOFF aniso"));	
				GUILayout.TextField(m_manager.m_skyNode.forceOFFaniso.ToString());
						
				if (GUILayout.Button ("Toggle"))
				{
					m_manager.m_skyNode.toggleAniso();
				}
				GUILayout.EndHorizontal ();
							



//				if (mr==null){
//				//								//Snippet from RbRay's EVE
//													Transform transform = GetScaledTransform (parentPlanet);													
//													{
//														mr = (MeshRenderer)transform.GetComponent (typeof(MeshRenderer));
//														if (mr != null)
//														{														
////															print ("planet shader: " + mr.material.shader);	
////															print("RENDER QUEUE"+mr.material.renderQueue);
//														}
//													}										
//				}




//				GUILayout.BeginHorizontal ();
//				GUILayout.Label ("RenderQueue Kerbin");
//				renderQueue2 = Convert.ToInt32 (GUILayout.TextField (renderQueue2.ToString ()));
//				
//				if (GUILayout.Button ("Set"))
//				{
//					mr.material.renderQueue = renderQueue2;
//				}
//				GUILayout.EndHorizontal ();

//				print("KERBIN RENDER QUEUE"+mr.material.renderQueue);

//				GUILayout.BeginHorizontal ();												
//				if (GUILayout.Button ("Enable Sun"))
//				{
//					Sun.Instance.sunFlare.gameObject.SetActive(true);
//				}
//
//
//				if (GUILayout.Button ("Disable Sun"))
//				{
//					Sun.Instance.sunFlare.gameObject.SetActive(false);
//				}
//				GUILayout.EndHorizontal ();
				
//				ScaledSpaceFader kerbinPsystemBody=ScaledSpace.Instance.transform.FindChild("Kerbin").gameObject.GetComponentInChildren<ScaledSpaceFader>();

//				if (kerbinPsystemBody == null) {
//					print ("NULL");
//				}
//				else{
//					print ("NOT NULL");
//					print("fadeStart");
//					
//					print(kerbinPsystemBody.fadeStart);
//					
//					print("fadeEnd");
//					
//					print(kerbinPsystemBody.fadeEnd);
//				}
				
//				GUILayout.BeginHorizontal ();
//				GUILayout.Label ("Fade Start");
//				fadeStart = Convert.ToInt32 (GUILayout.TextField (fadeStart.ToString ()));
//				
//				if (GUILayout.Button ("Set"))
//				{
//					kerbinPsystemBody.fadeStart=fadeStart;
//				}
//				GUILayout.EndHorizontal ();
//
//
//
//
//				
//				GUILayout.BeginHorizontal ();
//				GUILayout.Label ("Fade End");
//				fadeEnd = Convert.ToInt32 (GUILayout.TextField (fadeEnd.ToString ()));
//				
//				if (GUILayout.Button ("Set"))
//				{
//					kerbinPsystemBody.fadeEnd=fadeEnd;
//				}
//				GUILayout.EndHorizontal ();

//				print("FAR PLANE=");
//				print(cams[cam].farClipPlane);
//
//				print("near plane =");
//				print(cams[cam].nearClipPlane);

//				GUILayout.BeginHorizontal ();
//				GUILayout.Label ("Far Plane of current camera");
//				farplane = Convert.ToInt32 (GUILayout.TextField (farplane.ToString ()));
//				
//				if (GUILayout.Button ("Set"))
//				{
//					cams[cam].farClipPlane=farplane;
//				}
//				GUILayout.EndHorizontal ();
//
//
//				GUILayout.BeginHorizontal ();
//				GUILayout.Label ("Near Plane of current camera");
//				nearplane = Convert.ToInt32 (GUILayout.TextField (nearplane.ToString ()));
//				
//				if (GUILayout.Button ("Set"))
//				{
//					cams[cam].nearClipPlane=nearplane;
//				}
//				GUILayout.EndHorizontal ();


//				GUILayout.BeginHorizontal ();
//
//				GUILayout.Label ("Rim Blend (/100)");
//				rimBlend = (float)(Convert.ToDouble (GUILayout.TextField (rimBlend.ToString ())));
//
//				GUILayout.EndHorizontal ();
//
//
//				GUILayout.BeginHorizontal ();
//				
//				GUILayout.Label ("Rim Power (/100)");
//				rimpower = (float)(Convert.ToDouble (GUILayout.TextField (rimpower.ToString ())));
//				
//				GUILayout.EndHorizontal ();



//				if ((!MapView.MapIsEnabled)&&(!m_manager.m_skyNode.inScaledSpace))
//				{
//					DeactivateAtmosphere(parentPlanet);
//					//print ("STOCK ATMO DISABLED");
//				}
//
//				else
//				{
//					rimBlend=100f;   //kerbin settings, not sure about other planets
//					rimpower=600f;
//					ReactivateAtmosphere(parentPlanet,rimBlend,rimpower);
//					//print ("STOCK ATMO ENABLED");
//				
//				}

				
				GUILayout.BeginHorizontal ();
				GUILayout.Label ("ManagerState");
				GUILayout.TextField (m_manager.getManagerState ());
				GUILayout.EndHorizontal ();
				
				
//								GUILayout.BeginHorizontal ();
//								//Snippet from RbRay's EVE
//								if (GUILayout.Button ("Get Planet shader"))
//								{
//									Transform transform = GetScaledTransform (parentPlanet);
//									
//									PQS pqs = celestialBodies [PlanetId].pqsController;
//									
//									if (pqs != null)
//									{
//										MeshRenderer mr = (MeshRenderer)transform.GetComponent (typeof(MeshRenderer));
//										if (mr != null)
//										{														
//											print ("planet shader: " + mr.material.shader);														
//										}
//									}										
//								}
//								GUILayout.EndHorizontal ();
			
				chosenCamera=cams[cam];
			
			
			}
	}

		
		
		
		//snippet by Thomas P. from KSPforum
		public void DeactivateAtmosphere(string name)
		{
			Transform t = ScaledSpace.Instance.transform.FindChild(name);
			
			for (int i = 0; i < t.childCount; i++)
			{
				if (t.GetChild(i).gameObject.layer == 9)
				{
					// Deactivate the Athmosphere-renderer
					t.GetChild(i).gameObject.GetComponent<MeshRenderer>().gameObject.SetActive(false);
					
					// Reset the shader parameters
					Material sharedMaterial = t.renderer.sharedMaterial;



					//sharedMaterial.SetTexture(Shader.PropertyToID("_rimColorRamp"), null);
					sharedMaterial.SetFloat(Shader.PropertyToID("_rimBlend"), 0);
					sharedMaterial.SetFloat(Shader.PropertyToID("_rimPower"), 0);
					
					// Stop our script
					i = t.childCount + 10;
				}
			}
		}

			public void getSettingsFromSkynode() {

			extinctionCoeff = 10000 * m_manager.m_skyNode.extinctionCoeff;

			atmosphereGlobalScale = 1000 * m_manager.m_skyNode.atmosphereGlobalScale;
			postProcessingalpha = 100 * m_manager.m_skyNode.postProcessingAlpha;
			postProcessScale = 1000 * m_manager.m_skyNode.postProcessingScale;
			postProcessDepth = 10000 * m_manager.m_skyNode.postProcessDepth;


			postProcessExposure = 100* m_manager.m_skyNode.postProcessExposure;
			exposure = 100* m_manager.m_skyNode.m_HDRExposure;
			alphaCutoff = 10000 * m_manager.m_skyNode.alphaCutoff;
			alphaGlobal = 100 * m_manager.m_skyNode.alphaGlobal;


		}

		public void ReactivateAtmosphere(string name, float inRimBlend, float inRimPower)
		{
			Transform t = ScaledSpace.Instance.transform.FindChild(name);
			
			for (int i = 0; i < t.childCount; i++)
			{
				if (t.GetChild(i).gameObject.layer == 9)
				{
					// Reactivate the Athmosphere-renderer
					t.GetChild(i).gameObject.GetComponent<MeshRenderer>().gameObject.SetActive(true);
					
					// Reset the shader parameters
					Material sharedMaterial = t.renderer.sharedMaterial;
					
										
					//sharedMaterial.SetTexture(Shader.PropertyToID("_rimColorRamp"), null);
					sharedMaterial.SetFloat(Shader.PropertyToID("_rimBlend"), inRimBlend/100f);
					sharedMaterial.SetFloat(Shader.PropertyToID("_rimPower"), inRimPower/100f);
					
					// Stop our script
					i = t.childCount + 10;
				}
			}
		}

		public void savePlanetsList() {

			string codeBase = Assembly.GetExecutingAssembly().CodeBase;
			UriBuilder uri = new UriBuilder(codeBase);
			string path = Uri.UnescapeDataString(uri.Path);
			path=Path.GetDirectoryName (path);

			ConfigNode cnTemp = ConfigNode.CreateConfigFromObject(this);
			cnTemp.Save(path+"/config/PlanetsList.txt");
		}

		public void loadPlanetsList() {
			string codeBase = Assembly.GetExecutingAssembly().CodeBase;
			UriBuilder uri = new UriBuilder(codeBase);
			string path = Uri.UnescapeDataString(uri.Path);
			path=Path.GetDirectoryName (path);

			ConfigNode cnToLoad = ConfigNode.Load(path+"/config/PlanetsList.txt");

//			scattererPlanets=cnToLoad.

			ConfigNode.LoadObjectFromConfig(this, cnToLoad);


//			print(cnToLoad.GetValues ("scattererPlanets"));
//			for (int i=0; i<scattererPlanets.Length; i++) {
//				print (cnToLoad.GetValues ("scattererPlanets")[i]);
//				scattererPlanets[i] = cnToLoad.GetValues ("scattererPlanets")[i];
//			}

		}


	}
}