import {  makeAutoObservable,  } from "mobx";
import agent from "../api/agent";
import { Activity } from "../models/activity";

export default class ActivityStore  {
   activities: Activity[] = [];
   selectedActivity: Activity | null = null;
   editMode = false;
   loading = false;
   loadingInitial = false;

    constructor(){
        makeAutoObservable(this);
    }

    loadActivites = async() => {
        if(agent.IsSignedIn()){
        this.setLoadingInitial(true);
        try{
           const activities : Activity[] = await agent.Activities.list();
           console.log(activities);
           activities.forEach(activity =>{
             activity.category = 'Academic Calendar';
             activity.bodyPreview = activity.bodyPreview.split('\r')[0];
             this.activities.push(activity);
           })
           this.setLoadingInitial(false);
        }catch(error){
          console.log(error);
          this.setLoadingInitial(false);
        }
    }
  }

  setLoadingInitial = (state: boolean) => this.loadingInitial = state;

}