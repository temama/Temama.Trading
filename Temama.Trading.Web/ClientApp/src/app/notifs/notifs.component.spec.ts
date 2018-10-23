﻿/// <reference path="../../../../node_modules/@types/jasmine/index.d.ts" />
import { TestBed, async, ComponentFixture, ComponentFixtureAutoDetect } from '@angular/core/testing';
import { BrowserModule, By } from "@angular/platform-browser";
import { NotifsComponent } from './notifs.component';

let component: NotifsComponent;
let fixture: ComponentFixture<NotifsComponent>;

describe('notifs component', () => {
    beforeEach(async(() => {
        TestBed.configureTestingModule({
            declarations: [ NotifsComponent ],
            imports: [ BrowserModule ],
            providers: [
                { provide: ComponentFixtureAutoDetect, useValue: true }
            ]
        });
        fixture = TestBed.createComponent(NotifsComponent);
        component = fixture.componentInstance;
    }));

    it('should do something', async(() => {
        expect(true).toEqual(true);
    }));
});